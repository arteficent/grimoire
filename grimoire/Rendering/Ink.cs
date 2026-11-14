using Avalonia;
using Avalonia.Media;

namespace grimoire.Rendering;

/// <summary>
/// Pen-and-ink drawing helpers. Outlines wobble like a hand-held pen, start at a random spot and overshoot
/// a little where the pen lifts, and every stroke can be drawn on progressively.
/// </summary>
internal static class Ink
{
    /// <summary>Stable per-string seed so a shape keeps the same wobble from frame to frame.</summary>
    public static int Seed(string text)
    {
        unchecked
        {
            var h = (int)2166136261;
            foreach (var c in text)
            {
                h = (h ^ c) * 16777619;
            }

            return h;
        }
    }

    /// <summary>Deterministic noise in [-1, 1].</summary>
    public static double Noise(int seed, int i)
    {
        unchecked
        {
            var h = (uint)seed * 374761393u + (uint)i * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 32767.5 - 1;
        }
    }

    public static Pen Pen(Color color, double alpha, double thickness, IDashStyle? dash = null) =>
        new(Palette.Brush(color, alpha), thickness, dash, PenLineCap.Round, PenLineJoin.Round);

    // ---- outlines ---------------------------------------------------------------------------

    /// <summary>Points around a rounded rectangle, wobbling gently and overshooting the start.</summary>
    public static List<Point> RoundRect(Rect r, double radius, int seed, double wobble = 1.1)
    {
        radius = Math.Clamp(radius, 0, Math.Min(r.Width, r.Height) / 2);
        wobble = Math.Min(wobble, Math.Min(r.Width, r.Height) * 0.07);
        var path = new List<(Point P, Vector N)>();
        void Edge(Point a, Point b, Vector n)
        {
            var len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            var steps = Math.Max(1, (int)(len / 7));
            for (var i = 0; i < steps; i++)
            {
                var t = (double)i / steps;
                path.Add((new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t), n));
            }
        }

        void Arc(Point c, double from)
        {
            var steps = Math.Max(2, (int)(radius * 1.57 / 5));
            for (var i = 0; i < steps; i++)
            {
                var a = from + Math.PI / 2 * i / steps;
                var n = new Vector(Math.Cos(a), Math.Sin(a));
                path.Add((c + n * radius, n));
            }
        }

        Edge(new Point(r.Left + radius, r.Top), new Point(r.Right - radius, r.Top), new Vector(0, -1));
        Arc(new Point(r.Right - radius, r.Top + radius), -Math.PI / 2);
        Edge(new Point(r.Right, r.Top + radius), new Point(r.Right, r.Bottom - radius), new Vector(1, 0));
        Arc(new Point(r.Right - radius, r.Bottom - radius), 0);
        Edge(new Point(r.Right - radius, r.Bottom), new Point(r.Left + radius, r.Bottom), new Vector(0, 1));
        Arc(new Point(r.Left + radius, r.Bottom - radius), Math.PI / 2);
        Edge(new Point(r.Left, r.Bottom - radius), new Point(r.Left, r.Top + radius), new Vector(-1, 0));
        Arc(new Point(r.Left + radius, r.Top + radius), Math.PI);

        return Wobble(path, seed, wobble, closedOvershoot: 0.035);
    }

    /// <summary>Points around an ellipse, drawn in one loop that overlaps itself where the pen started.</summary>
    public static List<Point> Ellipse(Point c, double rx, double ry, int seed, double wobble = 1.1)
    {
        wobble = Math.Min(wobble, Math.Min(rx, ry) * 0.08);
        var n = (int)Math.Clamp((rx + ry) * 0.6, 20, 120);
        var start = Noise(seed, 1) * Math.PI;
        var path = new List<(Point, Vector)>(n);
        for (var i = 0; i < n; i++)
        {
            var a = start + 2 * Math.PI * i / n;
            var dir = new Vector(Math.Cos(a), Math.Sin(a));
            path.Add((new Point(c.X + rx * dir.X, c.Y + ry * dir.Y), dir));
        }

        return Wobble(path, seed, wobble, closedOvershoot: 0.06);
    }

    /// <summary>A slightly bowed pen line.</summary>
    public static List<Point> Line(Point a, Point b, int seed, double bow = 0.015)
    {
        var d = b - a;
        var len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        if (len < 0.5)
        {
            return [a, b];
        }

        var n = new Vector(-d.Y / len, d.X / len);
        var mid = a + d / 2 + n * len * bow * Noise(seed, 3);
        var j = Math.Min(0.8, len * 0.02);
        var a2 = a + new Vector(Noise(seed, 4), Noise(seed, 5)) * j;
        var b2 = b + new Vector(Noise(seed, 6), Noise(seed, 7)) * j;
        var steps = (int)Math.Clamp(len / 8, 3, 48);
        var points = new List<Point>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var u = 1 - t;
            points.Add(new Point(
                u * u * a2.X + 2 * u * t * mid.X + t * t * b2.X,
                u * u * a2.Y + 2 * u * t * mid.Y + t * t * b2.Y));
        }

        return points;
    }

    private static List<Point> Wobble(List<(Point P, Vector N)> path, int seed, double wobble, double closedOvershoot)
    {
        var n = path.Count;
        if (n < 2)
        {
            return [.. path.Select(p => p.P)];
        }

        var f1 = 2 + Math.Abs(Noise(seed, 11)) * 1.4;
        var f2 = 5 + Math.Abs(Noise(seed, 12)) * 2.5;
        var p1 = Noise(seed, 13) * Math.PI;
        var p2 = Noise(seed, 14) * Math.PI;
        var shift = (int)(Math.Abs(Noise(seed, 15)) * n) % n;
        var extra = (int)Math.Ceiling(n * closedOvershoot) + 1;
        var points = new List<Point>(n + extra);
        for (var i = 0; i < n + extra; i++)
        {
            var (p, normal) = path[(i + shift) % n];
            var t = (double)i / n * 2 * Math.PI;
            var offset = wobble * (0.65 * Math.Sin(t * f1 + p1) + 0.35 * Math.Sin(t * f2 + p2));
            // Lift the pen slightly inwards at the very end so the overlap reads as a pen stroke.
            if (i >= n)
            {
                offset -= wobble * 0.8 * (i - n + 1) / extra;
            }

            points.Add(p + normal * offset);
        }

        return points;
    }

    // ---- stroking ---------------------------------------------------------------------------

    /// <summary>Strokes the first <paramref name="progress"/> fraction (by length) of a polyline.</summary>
    public static void Stroke(DrawingContext dc, IReadOnlyList<Point> points, IPen pen, double progress = 1)
    {
        if (points.Count < 2 || progress <= 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(points[0], false);
            if (progress >= 1)
            {
                for (var i = 1; i < points.Count; i++)
                {
                    g.LineTo(points[i]);
                }
            }
            else
            {
                var total = 0.0;
                for (var i = 1; i < points.Count; i++)
                {
                    total += Distance(points[i - 1], points[i]);
                }

                var remaining = total * progress;
                for (var i = 1; i < points.Count; i++)
                {
                    var seg = Distance(points[i - 1], points[i]);
                    if (seg >= remaining)
                    {
                        var f = seg <= 0 ? 0 : remaining / seg;
                        g.LineTo(points[i - 1] + (points[i] - points[i - 1]) * f);
                        break;
                    }

                    g.LineTo(points[i]);
                    remaining -= seg;
                }
            }

            g.EndFigure(false);
        }

        dc.DrawGeometry(null, pen, geometry);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ---- composites -------------------------------------------------------------------------

    /// <summary>
    /// A hand-drawn box: a watercolour wash laid slightly off-register, a firm pen pass and a lighter
    /// second pass that does not quite follow the first.
    /// </summary>
    public static void Box(
        DrawingContext dc, Rect rect, double radius, Color ink, double inkAlpha, double thickness,
        Color? wash, double washAlpha, int seed, double progress = 1, IDashStyle? dash = null)
    {
        if (wash is { } w && washAlpha > 0)
        {
            var fade = Math.Clamp(progress * 1.6 - 0.4, 0, 1);
            var off = new Vector(Noise(seed, 21) * 1.6, Noise(seed, 22) * 1.3);
            dc.DrawRectangle(Palette.Brush(w, washAlpha * fade), null, rect.Translate(off), radius, radius);
        }

        Stroke(dc, RoundRect(rect, radius, seed), Pen(ink, inkAlpha, thickness, dash), progress);
        if (dash is null && thickness >= 1)
        {
            var second = Math.Clamp(progress * 1.25 - 0.25, 0, 1);
            Stroke(dc, RoundRect(rect.Inflate(0.7), radius, seed + 7, 1.6), Pen(ink, inkAlpha * 0.4, thickness * 0.55), second);
        }
    }

    /// <summary>A hand-drawn circle with an optional wash.</summary>
    public static void Circle(
        DrawingContext dc, Point center, double radius, Color ink, double inkAlpha, double thickness,
        Color? wash, double washAlpha, int seed, double progress = 1, IDashStyle? dash = null)
    {
        if (wash is { } w && washAlpha > 0)
        {
            var fade = Math.Clamp(progress * 1.6 - 0.4, 0, 1);
            var off = new Vector(Noise(seed, 21) * 1.5, Noise(seed, 22) * 1.2);
            dc.DrawEllipse(Palette.Brush(w, washAlpha * fade), null, center + off, radius, radius);
        }

        Stroke(dc, Ellipse(center, radius, radius, seed), Pen(ink, inkAlpha, thickness, dash), progress);
        if (dash is null && thickness >= 1)
        {
            var second = Math.Clamp(progress * 1.25 - 0.25, 0, 1);
            Stroke(dc, Ellipse(center, radius + 0.8, radius + 0.6, seed + 7, 1.4), Pen(ink, inkAlpha * 0.4, thickness * 0.55), second);
        }
    }

    /// <summary>An open arrowhead flicked with the pen.</summary>
    public static void Arrow(DrawingContext dc, Point tip, Vector direction, IPen pen, double size)
    {
        var len = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (len < 0.001)
        {
            return;
        }

        var d = direction / len;
        var n = new Vector(-d.Y, d.X);
        var back = tip - d * size;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(back + n * size * 0.55, false);
            g.LineTo(tip);
            g.LineTo(back - n * size * 0.5 - d * size * 0.08);
            g.EndFigure(false);
        }

        dc.DrawGeometry(null, pen, geometry);
    }

    /// <summary>Diagonal pen hatching inside a rounded rectangle (used for progress fills).</summary>
    public static void Hatch(DrawingContext dc, Rect rect, double radius, Color ink, double alpha, double spacing = 5.5)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using (dc.PushClip(new RoundedRect(rect, radius)))
        {
            var pen = new Pen(Palette.Brush(ink, alpha), 1.1, lineCap: PenLineCap.Round);
            for (var x = rect.X - rect.Height; x < rect.Right; x += spacing)
            {
                dc.DrawLine(pen, new Point(x, rect.Bottom), new Point(x + rect.Height, rect.Top));
            }
        }
    }

    /// <summary>A highlighter stroke: a slightly skewed band with uneven ends.</summary>
    public static void Marker(DrawingContext dc, Rect rect, Color color, double alpha, int seed)
    {
        if (alpha <= 0 || rect.Width <= 0)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(rect.X - 2 + Noise(seed, 31), rect.Y + 1.5 + Noise(seed, 32)), true);
            g.LineTo(new Point(rect.Right + 2 + Noise(seed, 33) * 2, rect.Y + Noise(seed, 34)));
            g.LineTo(new Point(rect.Right + Noise(seed, 35) * 2, rect.Bottom + Noise(seed, 36)));
            g.LineTo(new Point(rect.X + Noise(seed, 37), rect.Bottom - 1 + Noise(seed, 38)));
            g.EndFigure(true);
        }

        dc.DrawGeometry(Palette.Brush(color, alpha), null, geometry);
    }

    /// <summary>Draws text as if it were being written left to right, line by line.</summary>
    public static void Write(DrawingContext dc, FormattedText text, Point at, double progress)
    {
        if (progress >= 1)
        {
            dc.DrawText(text, at);
            return;
        }

        if (progress <= 0)
        {
            return;
        }

        var lineHeight = Math.Max(1, text.LineHeight > 0 ? text.LineHeight : text.Height);
        var lines = Math.Max(1, (int)Math.Round(text.Height / lineHeight));
        if (lines == 1)
        {
            lineHeight = text.Height;
        }

        var width = Math.Max(text.WidthIncludingTrailingWhitespace, text.Width) + 4;
        for (var i = 0; i < lines; i++)
        {
            var f = Math.Clamp(progress * lines - i, 0, 1);
            if (f <= 0)
            {
                break;
            }

            var maxWidth = text.MaxTextWidth > 0 ? Math.Max(width, text.MaxTextWidth) : width;
            using (dc.PushClip(new Rect(at.X - 4, at.Y + i * lineHeight - 2, maxWidth * f + 4, lineHeight + 4)))
            {
                dc.DrawText(text, at);
            }
        }
    }
}
