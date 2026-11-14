using Avalonia;
using Avalonia.Media;
using grimoire.Model;

namespace grimoire.Rendering;

/// <summary>Generic diagram rendering: boxes, circles, tables, regions and links in a 1000 x 560 virtual canvas.</summary>
public sealed partial class SceneCanvas
{
    private static readonly Color LinkNeutral = Color.Parse("#5B6B92");

    private void RenderDiagram(DrawingContext dc, SceneState scene, Rect bounds)
    {
        var (fx, fy, fw, fh) = scene.Frame ?? (0, 0, DiagramWidth, DiagramHeight);
        var fitAll = Math.Min(bounds.Width / DiagramWidth, bounds.Height / DiagramHeight);
        var scale = Math.Min(Math.Min(bounds.Width / fw, bounds.Height / fh), fitAll * 1.6);
        var ox = (bounds.Width - fw * scale) / 2 - fx * scale;
        var oy = (bounds.Height - fh * scale) / 2 - fy * scale;
        var area = new Rect(fx, fy, fw, fh);

        var layout = new Layout();
        var smoothing = Math.Min(1, _frameDt * 7);
        foreach (var s in scene.Shapes)
        {
            layout.Shapes[s.Id] = ApproachRect(s.Id, new Rect(s.X, s.Y, s.W, s.H), smoothing);
        }

        using (dc.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(ox, oy)))
        {
            foreach (var s in scene.Shapes.Where(s => s.Kind == ShapeKind.Region))
            {
                DrawRegion(dc, s, layout.Shapes[s.Id]);
            }

            foreach (var link in scene.Links)
            {
                DrawLink(dc, scene, link, layout);
            }

            foreach (var s in scene.Shapes.Where(s => s.Kind != ShapeKind.Region))
            {
                var rect = layout.Shapes[s.Id];
                var alpha = AppearAlpha(s.Id);
                switch (s.Kind)
                {
                    case ShapeKind.Box:
                        DrawBox(dc, s, rect, alpha);
                        break;
                    case ShapeKind.Circle:
                        DrawCircle(dc, s, rect, alpha);
                        break;
                    case ShapeKind.Table:
                        DrawTable(dc, s, rect, alpha);
                        break;
                    case ShapeKind.Label:
                        DrawLabel(dc, s, rect, alpha);
                        break;
                }
            }

            DrawTransfers(dc, scene, layout, hardwareOnly: false);

            if (!string.IsNullOrWhiteSpace(scene.Banner))
            {
                DrawBanner(dc, area, scene.Banner!, scene.BannerTone);
            }
        }
    }

    private Rect ApproachRect(string id, Rect target, double smoothing)
    {
        if (!_shapeRects.TryGetValue(id, out var current))
        {
            _shapeRects[id] = target;
            return target;
        }

        static double Lerp(double a, double b, double t) => Math.Abs(b - a) < 0.3 ? b : a + (b - a) * t;
        var next = new Rect(
            Lerp(current.X, target.X, smoothing),
            Lerp(current.Y, target.Y, smoothing),
            Math.Max(0, Lerp(current.Width, target.Width, smoothing)),
            Math.Max(0, Lerp(current.Height, target.Height, smoothing)));
        _shapeRects[id] = next;
        return next;
    }

    private double AppearAlpha(string id) =>
        _appear.TryGetValue(id, out var start) ? Math.Clamp((_time - start) / 0.45, 0, 1) : 1;

    private static (Color Fill, Color Border, Color Text) ShapeColors(Shape s)
    {
        if (s.Color >= 0)
        {
            var c = Palette.ForThread(s.Color);
            return (Palette.WithAlpha(c, 0.3), c, Palette.Text);
        }

        return s.Tone switch
        {
            Tone.Info => (Color.Parse("#1C2D5A"), Color.Parse("#7AA2FF"), Palette.Text),
            Tone.Good => (Color.Parse("#12352B"), Color.Parse("#34D399"), Palette.Text),
            Tone.Warn => (Color.Parse("#3A2E12"), Color.Parse("#FBBF24"), Palette.Text),
            Tone.Bad => (Color.Parse("#3B1520"), Color.Parse("#F43F5E"), Palette.Text),
            _ => (Color.Parse("#16213A"), Color.Parse("#34477A"), Palette.Text)
        };
    }

    private void DrawFlash(DrawingContext dc, Shape s, Rect rect, Color border, double radius)
    {
        var flash = FlashAmount("shape:" + s.Id);
        if (flash > 0)
        {
            var inflate = 3 + (1 - flash) * 9;
            dc.DrawRectangle(Palette.Brush(border, 0.35 * flash), null, rect.Inflate(inflate), radius + inflate, radius + inflate);
        }

        if (s.Tone == Tone.Bad && !s.Ghost)
        {
            var pulse = 0.18 + 0.14 * Math.Sin(_time * 5);
            dc.DrawRectangle(Palette.Brush(border, pulse), null, rect.Inflate(5), radius + 5, radius + 5);
        }
    }

    private void DrawBox(DrawingContext dc, Shape s, Rect rect, double alpha)
    {
        var (fill, border, text) = ShapeColors(s);
        var radius = Math.Min(10, rect.Height / 3);
        DrawFlash(dc, s, rect, border, radius);

        var fillAlpha = (s.Ghost ? 0.35 : 1) * alpha;
        var pen = s.Ghost
            ? new Pen(Palette.Brush(border, 0.6 * alpha), 1.4, new DashStyle([3, 3], 0))
            : new Pen(Palette.Brush(border, 0.95 * alpha), 1.6);
        dc.DrawRectangle(Palette.Brush(fill, fillAlpha), pen, rect, radius, radius);

        if (s.Fill > 0)
        {
            var bar = new Rect(rect.X + 2, rect.Y + 2, Math.Max(0, (rect.Width - 4) * s.Fill), rect.Height - 4);
            dc.DrawRectangle(Palette.Brush(border, 0.3 * alpha), null, bar, radius - 1, radius - 1);
        }

        DrawCenteredText(dc, s, rect, s.Ghost ? Palette.Muted : text, alpha);

        if (!string.IsNullOrEmpty(s.Tag))
        {
            var tag = Txt(s.Tag!, 10, Palette.WithAlpha(Palette.Muted, alpha), mono: true);
            dc.DrawText(tag, new Point(rect.Center.X - tag.Width / 2, rect.Y - tag.Height - 2));
        }
    }

    private void DrawCircle(DrawingContext dc, Shape s, Rect rect, double alpha)
    {
        var (fill, border, text) = ShapeColors(s);
        var flash = FlashAmount("shape:" + s.Id);
        var r = Math.Min(rect.Width, rect.Height) / 2;
        if (flash > 0)
        {
            var extra = 3 + (1 - flash) * 9;
            dc.DrawEllipse(Palette.Brush(border, 0.35 * flash), null, rect.Center, r + extra, r + extra);
        }

        if (s.Tone == Tone.Bad && !s.Ghost)
        {
            var pulse = 0.18 + 0.14 * Math.Sin(_time * 5);
            dc.DrawEllipse(Palette.Brush(border, pulse), null, rect.Center, r + 5, r + 5);
        }

        var pen = s.Ghost
            ? new Pen(Palette.Brush(border, 0.6 * alpha), 1.4, new DashStyle([3, 3], 0))
            : new Pen(Palette.Brush(border, 0.95 * alpha), 2);
        dc.DrawEllipse(Palette.Brush(fill, (s.Ghost ? 0.35 : 1) * alpha), pen, rect.Center, r, r);

        if (!string.IsNullOrEmpty(s.Text))
        {
            var maxWidth = r * 2 - 4;
            var t = Txt(s.Text!, s.FontSize, Palette.WithAlpha(s.Ghost ? Palette.Muted : text, alpha), FontWeight.SemiBold, s.Mono, maxWidth);
            t.TextAlignment = TextAlignment.Center;
            dc.DrawText(t, new Point(rect.Center.X - maxWidth / 2, rect.Center.Y - t.Height / 2));
        }

        if (!string.IsNullOrEmpty(s.Sub))
        {
            var sub = Txt(s.Sub!, 10.5, Palette.WithAlpha(Palette.Muted, alpha), maxWidth: 140);
            sub.TextAlignment = TextAlignment.Center;
            var bg = new Rect(rect.Center.X - sub.Width / 2 - 4, rect.Bottom + 2, sub.Width + 8, sub.Height + 2);
            dc.DrawRectangle(Palette.Brush(Palette.Background, 0.85 * alpha), null, bg, 4, 4);
            dc.DrawText(sub, new Point(rect.Center.X - 70, rect.Bottom + 3));
        }

        if (!string.IsNullOrEmpty(s.Tag))
        {
            var tag = Txt(s.Tag!, 10, Palette.WithAlpha(Palette.Muted, alpha), mono: true);
            dc.DrawText(tag, new Point(rect.Center.X - tag.Width / 2, rect.Y - tag.Height - 2));
        }
    }

    private static void DrawCenteredText(DrawingContext dc, Shape s, Rect rect, Color color, double alpha)
    {
        var maxWidth = Math.Max(10, rect.Width - 10);
        FormattedText? main = null;
        FormattedText? sub = null;
        if (!string.IsNullOrEmpty(s.Text))
        {
            main = Txt(s.Text!, s.FontSize, Palette.WithAlpha(color, alpha), FontWeight.SemiBold, s.Mono, maxWidth);
            main.TextAlignment = TextAlignment.Center;
        }

        if (!string.IsNullOrEmpty(s.Sub))
        {
            sub = Txt(s.Sub!, Math.Max(9.5, s.FontSize - 2.5), Palette.WithAlpha(Palette.Muted, alpha), maxWidth: maxWidth);
            sub.TextAlignment = TextAlignment.Center;
        }

        var total = (main?.Height ?? 0) + (sub?.Height ?? 0);
        var y = rect.Center.Y - total / 2;
        var x = rect.Center.X - maxWidth / 2;
        if (main is not null)
        {
            dc.DrawText(main, new Point(x, y));
            y += main.Height;
        }

        if (sub is not null)
        {
            dc.DrawText(sub, new Point(x, y));
        }
    }

    private void DrawLabel(DrawingContext dc, Shape s, Rect rect, double alpha)
    {
        var color = s.Tone == Tone.Neutral ? Palette.Muted : ShapeColors(s).Border;
        var weight = s.FontSize >= 15 ? FontWeight.SemiBold : FontWeight.Normal;
        var t = Txt(s.Text ?? string.Empty, s.FontSize, Palette.WithAlpha(color, alpha), weight, s.Mono, rect.Width);
        dc.DrawText(t, rect.TopLeft);
    }

    private void DrawTable(DrawingContext dc, Shape s, Rect rect, double alpha)
    {
        var (fill, border, _) = ShapeColors(s);
        DrawFlash(dc, s, rect, border, 8);
        dc.DrawRectangle(Palette.Brush(Palette.Panel, alpha), new Pen(Palette.Brush(border, 0.9 * alpha), 1.4), rect, 8, 8);
        dc.DrawRectangle(Palette.Brush(fill, alpha), null, new Rect(rect.X + 1, rect.Y + 1, rect.Width - 2, 28), 7, 7);
        dc.DrawText(TxtLine(s.Text ?? string.Empty, 12, Palette.WithAlpha(Palette.Text, alpha), rect.Width - 16, FontWeight.Bold), new Point(rect.X + 9, rect.Y + 6));

        var y = rect.Y + 30;
        for (var i = 0; i < s.Rows.Count; i++)
        {
            var row = new Rect(rect.X + 4, y, rect.Width - 8, 22);
            if (s.HotRows.Contains(i))
            {
                dc.DrawRectangle(Palette.Brush(Color.Parse("#7AA2FF"), 0.25 * alpha), null, row, 4, 4);
            }

            var color = s.HotRows.Contains(i) ? Palette.Text : Color.Parse("#B8C4E0");
            dc.DrawText(TxtLine(s.Rows[i], 12, Palette.WithAlpha(color, alpha), rect.Width - 18, mono: s.Mono), new Point(rect.X + 9, y + 3));
            y += 24;
        }
    }

    private void DrawRegion(DrawingContext dc, Shape s, Rect rect)
    {
        var border = s.Tone == Tone.Neutral ? Color.Parse("#2A3A60") : ShapeColors(s).Border;
        var alpha = AppearAlpha(s.Id);
        dc.DrawRectangle(
            Palette.Brush(border, 0.07 * alpha),
            new Pen(Palette.Brush(border, 0.55 * alpha), 1.2, new DashStyle([4, 3], 0)),
            rect,
            14,
            14);
        if (!string.IsNullOrEmpty(s.Text))
        {
            dc.DrawText(TxtLine(s.Text!.ToUpperInvariant(), 10.5, Palette.WithAlpha(border == Color.Parse("#2A3A60") ? Palette.Muted : border, alpha), rect.Width - 20, FontWeight.Bold), new Point(rect.X + 12, rect.Y + 8));
        }
    }

    private void DrawLink(DrawingContext dc, SceneState scene, Link link, Layout layout)
    {
        if (!layout.Shapes.TryGetValue(link.From, out var a) || !layout.Shapes.TryGetValue(link.To, out var b))
        {
            return;
        }

        var fromShape = scene.Shapes.First(s => s.Id == link.From);
        var toShape = scene.Shapes.First(s => s.Id == link.To);
        var from = Anchor(fromShape, a, b.Center);
        var to = Anchor(toShape, b, a.Center);
        var color = link.Tone == Tone.Neutral ? LinkNeutral : ShapeColors(new Shape { Id = string.Empty, Tone = link.Tone }).Border;
        var alpha = Math.Min(AppearAlpha(link.From), AppearAlpha(link.To));

        IDashStyle? dash = link.Animate
            ? new DashStyle([3, 2], -_time * 6)
            : link.Dashed ? new DashStyle([3, 3], 0) : null;
        dc.DrawLine(new Pen(Palette.Brush(color, 0.9 * alpha), link.Tone == Tone.Neutral ? 1.8 : 2.4, dash), from, to);
        if (link.Arrow)
        {
            DrawArrowHead(dc, to, to - from, Palette.WithAlpha(color, alpha), 8);
        }

        if (!string.IsNullOrEmpty(link.Label))
        {
            var mid = new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2);
            var label = Txt(link.Label!, 10.5, Palette.WithAlpha(color == LinkNeutral ? Palette.Muted : color, alpha), FontWeight.SemiBold);
            var bg = new Rect(mid.X - label.Width / 2 - 4, mid.Y - label.Height / 2 - 1, label.Width + 8, label.Height + 2);
            dc.DrawRectangle(Palette.Brush(Palette.Background, 0.9 * alpha), null, bg, 5, 5);
            dc.DrawText(label, new Point(bg.X + 4, bg.Y + 1));
        }
    }

    private static Point Anchor(Shape shape, Rect rect, Point toward)
    {
        if (shape.Kind != ShapeKind.Circle)
        {
            return EdgePoint(rect, toward);
        }

        var c = rect.Center;
        var d = toward - c;
        var len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        return len < 0.001 ? c : c + d / len * (rect.Width / 2);
    }
}
