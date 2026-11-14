using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace grimoire.Rendering;

public enum DoodleKind
{
    Moon,
    Sun,
    Sparkle,
    Stars,
    Planet,
    Constellation,
    MagicCircle,
    Flourish
}

/// <summary>Pen-drawn celestial and spell ornaments that ink themselves onto the page, then twinkle or turn slowly.</summary>
public sealed class Doodle : Control
{
    public static readonly StyledProperty<DoodleKind> KindProperty =
        AvaloniaProperty.Register<Doodle, DoodleKind>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<Doodle, IBrush?>(nameof(Stroke));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<Doodle, double>(nameof(Thickness), 1.4);

    private const double DrawSeconds = 1.8;
    private bool _attached;
    private TimeSpan? _start;
    private double _time;

    static Doodle()
    {
        AffectsRender<Doodle>(KindProperty, StrokeProperty, ThicknessProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<Doodle>(false);
    }

    public DoodleKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        InkReplay.Requested += Replay;
        Replay();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        InkReplay.Requested -= Replay;
        base.OnDetachedFromVisualTree(e);
    }

    private void Replay()
    {
        _start = null;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan now)
    {
        if (!_attached)
        {
            return;
        }

        _start ??= now;
        _time = (now - _start.Value).TotalSeconds;
        if (IsEffectivelyVisible)
        {
            InvalidateVisual();
        }

        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0 || Stroke is not ISolidColorBrush brush)
        {
            return;
        }

        var color = brush.Color;
        var progress = Math.Clamp(_time / DrawSeconds, 0, 1);
        progress = 1 - Math.Pow(1 - progress, 2);
        var seed = Ink.Seed(Kind.ToString() + Bounds.Width);
        var strokes = Build(Kind, _time, seed);

        var scaleX = Kind == DoodleKind.Flourish ? Bounds.Width / 200 : size / 100;
        var scaleY = Kind == DoodleKind.Flourish ? Bounds.Height / 40 : size / 100;
        var ox = Kind == DoodleKind.Flourish ? 0 : (Bounds.Width - size) / 2;
        var oy = Kind == DoodleKind.Flourish ? 0 : (Bounds.Height - size) / 2;

        var pen = Ink.Pen(color, 1, Thickness);
        var faint = Ink.Pen(color, 0.55, Thickness * 0.7);
        for (var i = 0; i < strokes.Count; i++)
        {
            // Strokes are inked one after another so the drawing appears the way it was drawn.
            var local = Math.Clamp(progress * strokes.Count - i * 0.6, 0, 1);
            if (local <= 0)
            {
                continue;
            }

            var (points, light) = strokes[i];
            var mapped = points.Select(p => new Point(ox + p.X * scaleX, oy + p.Y * scaleY)).ToList();
            Ink.Stroke(context, mapped, light ? faint : pen, local);
        }
    }

    // ---- drawings (100 x 100 units, flourish 200 x 40) ------------------------------------------

    private static List<(List<Point> Points, bool Light)> Build(DoodleKind kind, double t, int seed)
    {
        var s = new List<(List<Point>, bool)>();
        var c = new Point(50, 50);
        switch (kind)
        {
            case DoodleKind.Moon:
                s.Add((Crescent(c, 38, new Point(64, 40), 33), false));
                s.Add((Crescent(c, 34, new Point(62, 41), 31), true));
                s.Add((Sparkle(new Point(84, 74), 7 + Math.Sin(t * 2.2) * 1.5), false));
                s.Add((Sparkle(new Point(90, 18), 4 + Math.Sin(t * 2.9 + 1) * 1), true));
                break;

            case DoodleKind.Sun:
                s.Add((Ink.Ellipse(c, 20, 20, seed), false));
                s.Add((Ink.Ellipse(c, 15, 15, seed + 1, 0.6), true));
                for (var i = 0; i < 12; i++)
                {
                    var a = i * Math.PI / 6 + t * 0.15;
                    var r2 = i % 2 == 0 ? 44 : 36;
                    s.Add((Ink.Line(c + Dir(a) * 26, c + Dir(a) * r2, seed + i), i % 2 == 1));
                }

                break;

            case DoodleKind.Sparkle:
                s.Add((Sparkle(c, 40 + Math.Sin(t * 2) * 4), false));
                s.Add((Sparkle(new Point(82, 20), 10 + Math.Sin(t * 3 + 1) * 2), true));
                break;

            case DoodleKind.Stars:
                s.Add((Sparkle(new Point(30, 34), 20 + Math.Sin(t * 2.1) * 3), false));
                s.Add((Sparkle(new Point(72, 22), 11 + Math.Sin(t * 2.7 + 2) * 2), false));
                s.Add((Sparkle(new Point(70, 72), 14 + Math.Sin(t * 1.7 + 4) * 2), true));
                s.Add((Ink.Ellipse(new Point(18, 78), 2, 2, seed), true));
                s.Add((Ink.Ellipse(new Point(90, 48), 1.6, 1.6, seed + 1), true));
                break;

            case DoodleKind.Planet:
                s.Add((Ink.Ellipse(c, 24, 24, seed), false));
                s.Add((Ink.Line(new Point(32, 44), new Point(68, 40), seed + 2), true));
                s.Add((Ink.Line(new Point(30, 56), new Point(70, 52), seed + 3), true));
                s.Add((Ring(c, 46, 13, -0.35, front: true), false));
                s.Add((Sparkle(new Point(86, 16), 6 + Math.Sin(t * 2.4) * 1.5), true));
                break;

            case DoodleKind.Constellation:
                Point[] stars = [new(8, 70), new(28, 52), new(46, 60), new(62, 34), new(84, 26), new(92, 52)];
                for (var i = 1; i < stars.Length; i++)
                {
                    s.Add((Ink.Line(stars[i - 1], stars[i], seed + i), true));
                }

                for (var i = 0; i < stars.Length; i++)
                {
                    s.Add((Sparkle(stars[i], 4 + Math.Sin(t * 2 + i) * 1.2), false));
                }

                break;

            case DoodleKind.MagicCircle:
                AddMagicCircle(s, c, t, seed);
                break;

            case DoodleKind.Flourish:
                s.Add((Ink.Line(new Point(20, 20), new Point(88, 20), seed, 0.04), false));
                s.Add((Ink.Line(new Point(112, 20), new Point(180, 20), seed + 1, 0.04), false));
                s.Add((Spiral(new Point(20, 14), 6, Math.PI / 2, -1), false));
                s.Add((Spiral(new Point(180, 14), 6, Math.PI / 2, 1), false));
                s.Add((Diamond(new Point(100, 20), 9), false));
                s.Add((Sparkle(new Point(100, 20), 4), true));
                break;
        }

        return s;
    }

    private static void AddMagicCircle(List<(List<Point>, bool)> s, Point c, double t, int seed)
    {
        var spin = t * 0.12;
        s.Add((Ink.Ellipse(c, 48, 48, seed), false));
        s.Add((Ink.Ellipse(c, 44, 44, seed + 1, 0.5), true));
        s.Add((Ink.Ellipse(c, 34, 34, seed + 2), false));

        // Rune band between the outer rings, turning slowly.
        for (var i = 0; i < 24; i++)
        {
            var a = spin + i * Math.PI / 12;
            s.Add((Rune(c, a, 36.5, 41.5, seed + i), true));
        }

        // Hexagram counter-rotating inside.
        var hex = -spin * 1.6;
        Point Vertex(int k) => c + Dir(hex + k * Math.PI / 3 - Math.PI / 2) * 34;
        s.Add(([Vertex(0), Vertex(2), Vertex(4), Vertex(0)], false));
        s.Add(([Vertex(1), Vertex(3), Vertex(5), Vertex(1)], false));
        for (var k = 0; k < 6; k++)
        {
            var at = c + Dir(hex + k * Math.PI / 3 - Math.PI / 2) * 34;
            s.Add((Ink.Ellipse(at, 4, 4, seed + 40 + k, 0.3), true));
        }

        s.Add((Ink.Ellipse(c, 17, 17, seed + 3), false));
        s.Add((Crescent(c + new Vector(-2, 0), 9, c + new Vector(3, -2), 8), false));
        s.Add((Sparkle(c + new Vector(5, 2), 4 + Math.Sin(t * 3) * 1), true));
    }

    private static Vector Dir(double a) => new(Math.Cos(a), Math.Sin(a));

    private static double Dist(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static List<Point> Sparkle(Point c, double r)
    {
        var points = new List<Point>();
        for (var k = 0; k < 4; k++)
        {
            var a0 = k * Math.PI / 2 - Math.PI / 2;
            var tip = c + Dir(a0) * r;
            var next = c + Dir(a0 + Math.PI / 2) * r;
            for (var i = 0; i <= 8; i++)
            {
                var u = i / 8.0;
                var q = 1 - u;
                points.Add(new Point(q * q * tip.X + 2 * q * u * c.X + u * u * next.X, q * q * tip.Y + 2 * q * u * c.Y + u * u * next.Y));
            }
        }

        return points;
    }

    private static List<Point> Crescent(Point c1, double r1, Point c2, double r2)
    {
        var points = new List<Point>();
        for (var i = 0; i <= 60; i++)
        {
            var p = c1 + Dir(i * Math.PI / 30) * r1;
            if (Dist(p, c2) > r2)
            {
                points.Add(p);
            }
        }

        // Rotate so the outer arc is continuous, then close along the inner arc.
        var start = 0;
        for (var i = 1; i < points.Count; i++)
        {
            if (Dist(points[i], points[i - 1]) > r1 * 0.3)
            {
                start = i;
            }
        }

        var outer = points.Skip(start).Concat(points.Take(start)).ToList();
        if (outer.Count < 2)
        {
            return outer;
        }

        var a0 = Math.Atan2(outer[^1].Y - c2.Y, outer[^1].X - c2.X);
        var a1 = Math.Atan2(outer[0].Y - c2.Y, outer[0].X - c2.X);
        while (a1 < a0)
        {
            a1 += 2 * Math.PI;
        }

        // Pick the inner arc that stays inside the outer circle.
        var mid = c2 + Dir((a0 + a1) / 2) * r2;
        var forward = Dist(mid, c1) < r1;
        var span = forward ? a1 - a0 : -(2 * Math.PI - (a1 - a0));
        for (var i = 0; i <= 30; i++)
        {
            outer.Add(c2 + Dir(a0 + span * i / 30) * r2);
        }

        return outer;
    }

    private static List<Point> Ring(Point c, double rx, double ry, double tilt, bool front)
    {
        var points = new List<Point>();
        for (var i = 0; i <= 48; i++)
        {
            var a = i * Math.PI * 2 / 48;
            var x = Math.Cos(a) * rx;
            var y = Math.Sin(a) * ry;
            var p = new Point(c.X + x * Math.Cos(tilt) - y * Math.Sin(tilt), c.Y + x * Math.Sin(tilt) + y * Math.Cos(tilt));
            // Behind the planet the ring is hidden.
            if (!front || Math.Sin(a) > 0 || Dist(p, c) > 25)
            {
                points.Add(p);
            }
            else if (points.Count > 0)
            {
                break;
            }
        }

        return points;
    }

    private static List<Point> Rune(Point c, double angle, double r0, double r1, int seed)
    {
        var tangent = Dir(angle + Math.PI / 2);
        var basePt = c + Dir(angle) * r0;
        var top = c + Dir(angle) * r1;
        var w = 1.6;
        var shape = Math.Abs(seed) % 4;
        return shape switch
        {
            0 => [basePt, top, top + tangent * w, basePt + (top - basePt) * 0.5 + tangent * w],
            1 => [basePt - tangent * w, top, basePt + tangent * w],
            2 => [top - tangent * w, basePt, top + tangent * w],
            _ => [basePt, top, top - tangent * w, basePt + (top - basePt) * 0.4 - tangent * w, basePt + (top - basePt) * 0.4 + tangent * w]
        };
    }

    private static List<Point> Spiral(Point c, double r, double start, int direction)
    {
        var points = new List<Point>();
        for (var i = 0; i <= 30; i++)
        {
            var u = i / 30.0;
            var a = start + direction * u * Math.PI * 3;
            points.Add(c + Dir(a) * r * (1 - u * 0.8));
        }

        points.Reverse();
        return points;
    }

    private static List<Point> Diamond(Point c, double r) =>
        [c + new Vector(0, -r * 0.7), c + new Vector(r, 0), c + new Vector(0, r * 0.7), c + new Vector(-r, 0), c + new Vector(0, -r * 0.7)];
}
