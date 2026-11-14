using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using grimoire.Model;

namespace grimoire.Rendering;

/// <summary>
/// Immediate-mode renderer for a <see cref="SceneState"/>. Threads glide between positions, waiting
/// edges march, spinning threads spin, changed values flash and data packets travel over the bus.
/// </summary>
public sealed partial class SceneCanvas : Control
{
    public static readonly StyledProperty<SceneState?> SceneProperty =
        AvaloniaProperty.Register<SceneCanvas, SceneState?>(nameof(Scene));

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<SceneCanvas, double>(nameof(Speed), 1.0);

    /// <summary>Changing this value drops all animation state (used when switching lessons).</summary>
    public static readonly StyledProperty<int> EpochProperty =
        AvaloniaProperty.Register<SceneCanvas, int>(nameof(Epoch));

    private const double PillW = 178;
    private const double PillH = 36;
    private const double BoxW = 176;
    private const double BoxH = 74;
    private const double FlashSeconds = 1.4;
    private const double PacketSeconds = 1.8;
    private const double SoftwareMinWidth = 960;
    private const double SoftwareMinHeight = 500;
    private const double DiagramWidth = 1000;
    private const double DiagramHeight = 560;

    private static readonly FontFamily MonoFamily = new("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace");

    private readonly Dictionary<string, Point> _threadPos = [];
    private readonly Dictionary<string, Point> _resourcePos = [];
    private readonly Dictionary<string, Rect> _shapeRects = [];
    private readonly Dictionary<string, double> _appear = [];
    private readonly Dictionary<string, double> _flash = [];
    private bool _attached;
    private TimeSpan? _lastFrame;
    private double _time;
    private double _frameDt;
    private double _transferStart;

    static SceneCanvas()
    {
        AffectsRender<SceneCanvas>(SceneProperty);
        ClipToBoundsProperty.OverrideDefaultValue<SceneCanvas>(true);
    }

    public SceneState? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public int Epoch
    {
        get => GetValue(EpochProperty);
        set => SetValue(EpochProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _lastFrame = null;
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EpochProperty)
        {
            _threadPos.Clear();
            _resourcePos.Clear();
            _shapeRects.Clear();
            _appear.Clear();
            _flash.Clear();
        }
        else if (change.Property == SceneProperty)
        {
            TrackChanges(change.GetOldValue<SceneState?>(), change.GetNewValue<SceneState?>());
        }
    }

    private void RequestFrame() => TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);

    private void OnFrame(TimeSpan timestamp)
    {
        if (!_attached)
        {
            return;
        }

        var dt = _lastFrame is { } last ? (timestamp - last).TotalSeconds : 0;
        _lastFrame = timestamp;
        _frameDt = Math.Clamp(dt, 0, 0.1) * Math.Max(0.1, Speed);
        _time += _frameDt;
        InvalidateVisual();
        RequestFrame();
    }

    private void TrackChanges(SceneState? previous, SceneState? next)
    {
        _transferStart = _time;
        if (next is null)
        {
            return;
        }

        foreach (var r in next.Resources)
        {
            var old = previous?.Resources.FirstOrDefault(o => o.Id == r.Id);
            if (old is null
                || old.Value != r.Value
                || old.Tone != r.Tone
                || !old.Items.SequenceEqual(r.Items)
                || !old.Owners.SequenceEqual(r.Owners)
                || !old.Waiters.SequenceEqual(r.Waiters))
            {
                _flash["res:" + r.Id] = _time;
            }
        }

        foreach (var c in next.Cores)
        {
            var old = previous?.Cores.FirstOrDefault(o => o.Index == c.Index);
            if (old is null || old.ThreadId != c.ThreadId || old.Register != c.Register || !old.Cache.SequenceEqual(c.Cache))
            {
                _flash["core:" + c.Index] = _time;
            }
        }

        foreach (var m in next.Memory)
        {
            var old = previous?.Memory.FirstOrDefault(o => o.Key == m.Key);
            if (old is null || old != m)
            {
                _flash["ram:" + m.Key] = _time;
            }
        }

        foreach (var s in next.Shapes)
        {
            var old = previous?.Shapes.FirstOrDefault(o => o.Id == s.Id);
            if (old is null)
            {
                _appear[s.Id] = _time;
                continue;
            }

            if (old.Text != s.Text
                || old.Sub != s.Sub
                || old.Tone != s.Tone
                || old.Ghost != s.Ghost
                || Math.Abs(old.Fill - s.Fill) > 0.001
                || !old.Rows.SequenceEqual(s.Rows)
                || !old.HotRows.SetEquals(s.HotRows))
            {
                _flash["shape:" + s.Id] = _time;
            }
        }
    }

    // =========================================================================================
    // Rendering
    // =========================================================================================

    public override void Render(DrawingContext dc)
    {
        var bounds = new Rect(Bounds.Size);
        dc.FillRectangle(Palette.Brush(Palette.Background), bounds);
        DrawDotGrid(dc, bounds);

        var scene = Scene;
        if (scene is null)
        {
            var hint = Txt("Pick a scenario on the left to start the walkthrough.", 16, Palette.Muted);
            dc.DrawText(hint, new Point((bounds.Width - hint.Width) / 2, (bounds.Height - hint.Height) / 2));
            return;
        }

        if (scene.Shapes.Count > 0)
        {
            RenderDiagram(dc, scene, bounds);
            return;
        }

        var hasHardware = scene.Cores.Count > 0 || scene.Memory.Count > 0;
        var softwareHeight = hasHardware ? Math.Clamp(bounds.Height * 0.64, Math.Min(330, bounds.Height), Math.Max(330, bounds.Height - 210)) : bounds.Height;
        var hardware = new Rect(0, softwareHeight, bounds.Width, Math.Max(0, bounds.Height - softwareHeight));

        // The software view is authored for a minimum virtual size and scaled down to fit smaller windows.
        var scale = Math.Min(1, Math.Min(bounds.Width / SoftwareMinWidth, softwareHeight / SoftwareMinHeight));
        var software = new Rect(0, 0, bounds.Width / scale, softwareHeight / scale);

        var layout = new Layout();
        var deadlocked = scene.FindDeadlockedThreads();

        if (hasHardware)
        {
            LayoutHardware(scene, hardware, layout);
            dc.DrawLine(new Pen(Palette.Brush(Palette.PanelBorder), 1), new Point(12, hardware.Y), new Point(bounds.Width - 12, hardware.Y));
            DrawSectionCaption(dc, new Point(18, hardware.Y + 10), "HARDWARE VIEW", "CPU cores, private L1 caches (MESI), coherence bus and DRAM");
            DrawHardware(dc, scene, layout);
            DrawTransfers(dc, scene, layout, hardwareOnly: true);
        }

        using (dc.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            LayoutSoftware(scene, software, layout);
            DrawSectionCaption(dc, new Point(18, 12), "SOFTWARE VIEW", "threads and the synchronisation objects they hold or wait on");

            var holdLabels = DrawEdges(dc, scene, layout, deadlocked);
            foreach (var r in scene.Resources)
            {
                DrawResource(dc, r, layout.Resources[r.Id]);
            }

            foreach (var t in scene.Threads)
            {
                DrawThread(dc, t, layout.Threads[t.Id], deadlocked.Contains(t.Id));
            }

            DrawHoldLabels(dc, holdLabels, layout);

            DrawTransfers(dc, scene, layout, hardwareOnly: false);

            foreach (var t in scene.Threads.Where(t => !string.IsNullOrWhiteSpace(t.Note)))
            {
                DrawBubble(dc, layout.Threads[t.Id], t.Note!, Palette.ForThread(t.Color), software);
            }

            if (!string.IsNullOrWhiteSpace(scene.Banner))
            {
                DrawBanner(dc, software, scene.Banner!, scene.BannerTone);
            }
        }
    }

    private sealed class Layout
    {
        public Dictionary<string, Rect> Threads { get; } = [];
        public Dictionary<string, Rect> Resources { get; } = [];
        public Dictionary<int, Rect> Cores { get; } = [];
        public Dictionary<string, Rect> MemCells { get; } = [];
        public Dictionary<string, Rect> Shapes { get; } = [];
        public Rect Ram { get; set; }
        public double BusY { get; set; }
    }

    // ---- layout -----------------------------------------------------------------------------

    private void LayoutSoftware(SceneState scene, Rect area, Layout layout)
    {
        var laneX = area.X + 18;
        var laneTop = area.Y + 50;
        var regionLeft = laneX + PillW + 50 + BoxW / 2;
        var regionRight = area.Right - 24 - BoxW / 2;
        var regionTop = area.Y + 50 + PillH + 26 + BoxH / 2;
        var regionBottom = area.Bottom - 64 - (PillH + 8) * 2 - 18 - BoxH / 2;
        if (regionBottom < regionTop)
        {
            regionBottom = regionTop;
        }

        var smoothing = Math.Min(1, _frameDt * 7);

        foreach (var r in scene.Resources)
        {
            var width = ResourceWidth(r);
            var target = new Point(
                regionLeft + r.X * Math.Max(0, regionRight - regionLeft),
                regionTop + r.Y * (regionBottom - regionTop));
            var center = Approach(_resourcePos, r.Id, target, smoothing);
            layout.Resources[r.Id] = new Rect(center.X - width / 2, center.Y - BoxH / 2, width, BoxH);
        }

        // Which threads sit above each resource (operating on it) or below it (queued on it).
        var above = new Dictionary<string, List<string>>();
        var below = new Dictionary<string, List<string>>();
        foreach (var r in scene.Resources)
        {
            below[r.Id] = [.. r.Waiters];
            above[r.Id] = [];
        }

        foreach (var t in scene.Threads)
        {
            var isWaiter = scene.Resources.Any(r => r.Waiters.Contains(t.Id));
            if (!isWaiter && t.At is { } at && above.TryGetValue(at, out var list))
            {
                list.Add(t.Id);
            }
        }

        var lane = 0;
        foreach (var t in scene.Threads)
        {
            Point target;
            var waitingOn = scene.Resources.FirstOrDefault(r => r.Waiters.Contains(t.Id));
            if (waitingOn is not null)
            {
                var box = layout.Resources[waitingOn.Id];
                var index = below[waitingOn.Id].IndexOf(t.Id);
                target = new Point(box.Center.X - PillW / 2, box.Bottom + 34 + index * (PillH + 8));
            }
            else if (t.At is { } at && layout.Resources.TryGetValue(at, out var box))
            {
                var group = above[at];
                var index = group.IndexOf(t.Id);
                var columns = Math.Min(group.Count, 3);
                var row = index / columns;
                var inRow = Math.Min(columns, group.Count - row * columns);
                var col = index % columns;
                var total = inRow * PillW + (inRow - 1) * 8;
                target = new Point(box.Center.X - total / 2 + col * (PillW + 8), box.Y - 16 - PillH - row * (PillH + 10));
            }
            else
            {
                target = new Point(laneX, laneTop + lane * (PillH + 22));
            }

            lane++;
            var pos = Approach(_threadPos, t.Id, target, smoothing);
            layout.Threads[t.Id] = new Rect(pos.X, pos.Y, PillW, PillH);
        }
    }

    private static Point Approach(Dictionary<string, Point> store, string key, Point target, double smoothing)
    {
        if (!store.TryGetValue(key, out var current))
        {
            store[key] = target;
            return target;
        }

        var next = new Point(current.X + (target.X - current.X) * smoothing, current.Y + (target.Y - current.Y) * smoothing);
        if (Math.Abs(next.X - target.X) < 0.3 && Math.Abs(next.Y - target.Y) < 0.3)
        {
            next = target;
        }

        store[key] = next;
        return next;
    }

    private static double ResourceWidth(ResourceNode r)
    {
        if (r.Items.Count == 0)
        {
            return BoxW;
        }

        var chips = Math.Min(r.Items.Count, 6);
        var width = 20 + r.Items.Take(chips).Sum(i => Math.Min(110, 18 + i.Length * 6.6) + 5);
        return Math.Clamp(width, BoxW, 380);
    }

    private static void LayoutHardware(SceneState scene, Rect area, Layout layout)
    {
        var top = area.Y + 36;
        var busY = area.Bottom - 22;
        var coreBottom = busY - 14;
        var ramLeft = area.Right - Math.Min(300, area.Width * 0.27) - 18;
        var coresRight = ramLeft - 44;
        var n = Math.Max(1, scene.Cores.Count);
        var gap = 14.0;
        var coreW = Math.Min(280, (coresRight - 18 - (n - 1) * gap) / n);

        for (var i = 0; i < scene.Cores.Count; i++)
        {
            layout.Cores[scene.Cores[i].Index] = new Rect(18 + i * (coreW + gap), top, coreW, Math.Max(60, coreBottom - top));
        }

        layout.BusY = busY;
        layout.Ram = new Rect(ramLeft, top, area.Right - 18 - ramLeft, Math.Max(60, busY + 10 - top));
        for (var i = 0; i < scene.Memory.Count; i++)
        {
            layout.MemCells[scene.Memory[i].Key] = new Rect(layout.Ram.X + 10, layout.Ram.Y + 30 + i * 24, layout.Ram.Width - 20, 21);
        }
    }

    // ---- primitives -------------------------------------------------------------------------

    private static FormattedText Txt(string text, double size, Color color, FontWeight weight = FontWeight.Normal, bool mono = false, double maxWidth = 0)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(mono ? MonoFamily : FontFamily.Default, FontStyle.Normal, weight),
            size,
            Palette.Brush(color));
        if (maxWidth > 0)
        {
            ft.MaxTextWidth = maxWidth;
        }

        return ft;
    }

    private static FormattedText TxtLine(string text, double size, Color color, double maxWidth, FontWeight weight = FontWeight.Normal, bool mono = false)
    {
        var ft = Txt(text, size, color, weight, mono, Math.Max(10, maxWidth));
        ft.MaxLineCount = 1;
        ft.Trimming = TextTrimming.CharacterEllipsis;
        return ft;
    }

    private double FlashAmount(string key)
    {
        if (!_flash.TryGetValue(key, out var start))
        {
            return 0;
        }

        var f = 1 - (_time - start) / FlashSeconds;
        return f > 0 ? f : 0;
    }

    private static void DrawDotGrid(DrawingContext dc, Rect bounds)
    {
        var brush = Palette.Brush(Palette.Grid);
        for (var x = 12.0; x < bounds.Width; x += 28)
        {
            for (var y = 12.0; y < bounds.Height; y += 28)
            {
                dc.FillRectangle(brush, new Rect(x, y, 1.6, 1.6));
            }
        }
    }

    private static void DrawSectionCaption(DrawingContext dc, Point at, string title, string subtitle)
    {
        var t = Txt(title, 11, Palette.Muted, FontWeight.Bold);
        dc.DrawText(t, at);
        dc.DrawText(Txt("  ·  " + subtitle, 11, Palette.Dim), new Point(at.X + t.Width, at.Y));
    }

    private static void DrawArrowHead(DrawingContext dc, Point tip, Vector direction, Color color, double size = 9)
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
            g.BeginFigure(tip, true);
            g.LineTo(back + n * size * 0.55);
            g.LineTo(back - n * size * 0.55);
            g.EndFigure(true);
        }

        dc.DrawGeometry(Palette.Brush(color), null, geometry);
    }

    /// <summary>Point on the border of <paramref name="rect"/> along the line towards <paramref name="toward"/>.</summary>
    private static Point EdgePoint(Rect rect, Point toward)
    {
        var c = rect.Center;
        var dx = toward.X - c.X;
        var dy = toward.Y - c.Y;
        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
        {
            return c;
        }

        var sx = Math.Abs(dx) > 0.001 ? rect.Width / 2 / Math.Abs(dx) : double.MaxValue;
        var sy = Math.Abs(dy) > 0.001 ? rect.Height / 2 / Math.Abs(dy) : double.MaxValue;
        var s = Math.Min(sx, sy);
        return new Point(c.X + dx * s, c.Y + dy * s);
    }

    // ---- edges ------------------------------------------------------------------------------

    private List<(Point Mid, string Text)> DrawEdges(DrawingContext dc, SceneState scene, Layout layout, HashSet<string> deadlocked)
    {
        var labels = new List<(Point, string)>();
        foreach (var r in scene.Resources)
        {
            var box = layout.Resources[r.Id];
            foreach (var owner in r.Owners)
            {
                if (!layout.Threads.TryGetValue(owner, out var pill))
                {
                    continue;
                }

                var inCycle = deadlocked.Contains(owner);
                var from = EdgePoint(pill, box.Center);
                var to = EdgePoint(box, pill.Center);
                var pen = new Pen(Palette.Brush(Palette.Hold, inCycle ? 0.95 : 0.8), inCycle ? 3.2 : 2.4);
                dc.DrawLine(pen, from, to);
                dc.DrawEllipse(Palette.Brush(Palette.Hold), null, to, 4, 4);

                var length = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
                if (length > 140)
                {
                    labels.Add((new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), "holds " + r.Label));
                }
            }

            foreach (var waiter in r.Waiters)
            {
                if (!layout.Threads.TryGetValue(waiter, out var pill))
                {
                    continue;
                }

                var inCycle = deadlocked.Contains(waiter);
                var from = EdgePoint(pill, box.Center);
                var to = EdgePoint(box, pill.Center);
                var dash = new DashStyle([3, 2], -_time * 6);
                var pen = new Pen(Palette.Brush(Palette.Wait, inCycle ? 1 : 0.85), inCycle ? 3 : 2.2, dash);
                dc.DrawLine(pen, from, to);
                DrawArrowHead(dc, to, to - from, Palette.Wait);
            }
        }

        return labels;
    }

    private static void DrawHoldLabels(DrawingContext dc, List<(Point Mid, string Text)> labels, Layout layout)
    {
        foreach (var (mid, text) in labels)
        {
            var label = Txt(text, 10.5, Palette.Hold, FontWeight.SemiBold);
            var bg = new Rect(mid.X - label.Width / 2 - 5, mid.Y - label.Height / 2 - 2, label.Width + 10, label.Height + 4);
            if (layout.Resources.Values.Any(r => r.Intersects(bg)) || layout.Threads.Values.Any(t => t.Intersects(bg)))
            {
                continue;
            }

            dc.DrawRectangle(Palette.Brush(Palette.Background, 0.85), null, bg, 6, 6);
            dc.DrawText(label, new Point(bg.X + 5, bg.Y + 2));
        }
    }

    // ---- resources --------------------------------------------------------------------------

    private void DrawResource(DrawingContext dc, ResourceNode r, Rect box)
    {
        var kindColor = Palette.ForKind(r.Kind);
        var flash = FlashAmount("res:" + r.Id);
        var toneColor = r.Tone == Tone.Neutral ? kindColor : Palette.ForTone(r.Tone);

        if (flash > 0)
        {
            var inflate = 4 + (1 - flash) * 10;
            dc.DrawRectangle(Palette.Brush(toneColor, 0.35 * flash), null, box.Inflate(inflate), 16, 16);
        }

        if (r.Tone == Tone.Bad)
        {
            var pulse = 0.25 + 0.2 * Math.Sin(_time * 5);
            dc.DrawRectangle(Palette.Brush(Palette.Wait, pulse), null, box.Inflate(6), 15, 15);
        }

        dc.DrawRectangle(
            Palette.Brush(Palette.Panel),
            new Pen(Palette.Brush(toneColor, r.Tone == Tone.Neutral ? 0.7 : 1), r.Tone == Tone.Neutral ? 1.5 : 2.5),
            box,
            12,
            12);
        dc.DrawRectangle(Palette.Brush(kindColor), null, new Rect(box.X + 1, box.Y + 10, 3, box.Height - 20), 1.5, 1.5);

        var inner = box.Width - 24;
        dc.DrawText(TxtLine(Palette.KindLabel(r.Kind), 9, kindColor, inner, FontWeight.Bold), new Point(box.X + 12, box.Y + 7));
        dc.DrawText(TxtLine(r.Label, 14, Palette.Text, inner, FontWeight.SemiBold), new Point(box.X + 12, box.Y + 20));

        var y = box.Y + 41;
        if (r.Items.Count > 0)
        {
            var x = box.X + 12;
            var shown = 0;
            foreach (var item in r.Items)
            {
                var label = TxtLine(item, 10, Palette.Background, 100, FontWeight.SemiBold);
                var w = Math.Min(110, label.Width + 12);
                if (x + w > box.Right - 8 || shown == 6)
                {
                    dc.DrawText(Txt($"+{r.Items.Count - shown}", 10, Palette.Muted, FontWeight.Bold), new Point(x, y + 3));
                    break;
                }

                dc.DrawRectangle(Palette.Brush(kindColor, 0.9), null, new Rect(x, y, w, 18), 9, 9);
                dc.DrawText(label, new Point(x + 6, y + 2));
                x += w + 5;
                shown++;
            }
        }
        else if (!string.IsNullOrEmpty(r.Value))
        {
            dc.DrawText(TxtLine(r.Value!, 12, r.Tone == Tone.Neutral ? Palette.Muted : toneColor, inner, FontWeight.Normal, mono: true), new Point(box.X + 12, y + 1));
        }

        if (!string.IsNullOrEmpty(r.Caption))
        {
            var caption = TxtLine(r.Caption!, 10, Palette.Dim, inner);
            dc.DrawText(caption, new Point(box.Right - caption.Width - 10, box.Y + 7));
        }

        if (r.Items.Count > 0 && !string.IsNullOrEmpty(r.Value))
        {
            var value = TxtLine(r.Value!, 10, r.Tone == Tone.Neutral ? Palette.Muted : toneColor, inner, mono: true);
            dc.DrawText(value, new Point(box.X + 12, box.Bottom + 3));
        }
    }

    // ---- threads ----------------------------------------------------------------------------

    private void DrawThread(DrawingContext dc, ThreadNode t, Rect pill, bool deadlocked)
    {
        var color = Palette.ForThread(t.Color);
        var phaseColor = Palette.ForPhase(t.Phase);
        var faded = t.Phase == ThreadPhase.Done;

        if (deadlocked)
        {
            var pulse = 0.35 + 0.25 * Math.Sin(_time * 5);
            dc.DrawRectangle(Palette.Brush(Palette.Wait, pulse), null, pill.Inflate(7), 24, 24);
        }
        else if (t.Phase == ThreadPhase.Running)
        {
            var pulse = 0.12 + 0.08 * Math.Sin(_time * 4);
            dc.DrawRectangle(Palette.Brush(phaseColor, pulse), null, pill.Inflate(4), 22, 22);
        }

        dc.DrawRectangle(
            Palette.Brush(color, faded ? 0.18 : 0.22),
            new Pen(Palette.Brush(faded ? Palette.Dim : phaseColor), 2),
            pill,
            18,
            18);
        dc.DrawEllipse(Palette.Brush(color, faded ? 0.4 : 1), null, new Point(pill.X + 16, pill.Center.Y), 6, 6);

        var textWidth = pill.Width - 52;
        dc.DrawText(TxtLine(t.Name, 12, faded ? Palette.Muted : Palette.Text, textWidth, FontWeight.SemiBold), new Point(pill.X + 28, pill.Y + 3));
        dc.DrawText(TxtLine(Palette.Describe(t.Phase), 8.5, phaseColor, textWidth, FontWeight.Bold), new Point(pill.X + 28, pill.Y + 20));

        DrawPhaseIcon(dc, t.Phase, new Point(pill.Right - 16, pill.Center.Y), phaseColor);
    }

    private void DrawPhaseIcon(DrawingContext dc, ThreadPhase phase, Point c, Color color)
    {
        var brush = Palette.Brush(color);
        switch (phase)
        {
            case ThreadPhase.Running:
                dc.DrawEllipse(brush, null, c, 4 + Math.Sin(_time * 6) * 1.2, 4 + Math.Sin(_time * 6) * 1.2);
                break;

            case ThreadPhase.Spinning:
                {
                    var angle = _time * 9;
                    const double r = 7;
                    var start = new Point(c.X + r * Math.Cos(angle), c.Y + r * Math.Sin(angle));
                    var end = new Point(c.X + r * Math.Cos(angle + 4.6), c.Y + r * Math.Sin(angle + 4.6));
                    var geometry = new StreamGeometry();
                    using (var g = geometry.Open())
                    {
                        g.BeginFigure(start, false);
                        g.ArcTo(end, new Size(r, r), 0, true, SweepDirection.Clockwise);
                        g.EndFigure(false);
                    }

                    dc.DrawGeometry(null, new Pen(brush, 2.4, lineCap: PenLineCap.Round), geometry);
                    break;
                }

            case ThreadPhase.Blocked:
                {
                    // Padlock: shackle + body.
                    var shackle = new StreamGeometry();
                    using (var g = shackle.Open())
                    {
                        g.BeginFigure(new Point(c.X - 4, c.Y - 1), false);
                        g.LineTo(new Point(c.X - 4, c.Y - 4));
                        g.ArcTo(new Point(c.X + 4, c.Y - 4), new Size(4, 4), 0, false, SweepDirection.Clockwise);
                        g.LineTo(new Point(c.X + 4, c.Y - 1));
                        g.EndFigure(false);
                    }

                    dc.DrawGeometry(null, new Pen(brush, 1.8), shackle);
                    dc.DrawRectangle(brush, null, new Rect(c.X - 6, c.Y - 1, 12, 9), 2, 2);
                    break;
                }

            case ThreadPhase.Waiting:
                {
                    var ring = 5 + (_time * 6 % 6);
                    dc.DrawEllipse(null, new Pen(Palette.Brush(color, 1 - (ring - 5) / 6), 1.6), c, ring, ring);
                    dc.DrawEllipse(brush, null, c, 3, 3);
                    break;
                }

            case ThreadPhase.Sleeping:
                {
                    var bob = Math.Sin(_time * 2.5) * 2;
                    dc.DrawText(Txt("z", 10, color, FontWeight.Bold), new Point(c.X - 7, c.Y - 3 + bob));
                    dc.DrawText(Txt("z", 13, color, FontWeight.Bold), new Point(c.X - 1, c.Y - 11 - bob));
                    break;
                }

            case ThreadPhase.Done:
                {
                    var geometry = new StreamGeometry();
                    using (var g = geometry.Open())
                    {
                        g.BeginFigure(new Point(c.X - 5, c.Y), false);
                        g.LineTo(new Point(c.X - 1, c.Y + 4));
                        g.LineTo(new Point(c.X + 6, c.Y - 5));
                        g.EndFigure(false);
                    }

                    dc.DrawGeometry(null, new Pen(Palette.Brush(Palette.Hold), 2.2, lineCap: PenLineCap.Round), geometry);
                    break;
                }

            case ThreadPhase.Faulted:
                dc.DrawEllipse(brush, null, c, 7, 7);
                var bang = Txt("!", 11, Palette.Background, FontWeight.Bold);
                dc.DrawText(bang, new Point(c.X - bang.Width / 2, c.Y - bang.Height / 2));
                break;

            default:
                dc.DrawEllipse(null, new Pen(brush, 1.5), c, 4, 4);
                break;
        }
    }

    private static void DrawBubble(DrawingContext dc, Rect pill, string note, Color color, Rect bounds)
    {
        var text = Txt(note, 11.5, Palette.Text, maxWidth: 230);
        var w = text.Width + 18;
        var h = text.Height + 10;
        var x = pill.Right + 12;
        if (x + w > bounds.Right - 6)
        {
            x = pill.X - w - 12;
        }

        var y = pill.Center.Y - h / 2;
        y = Math.Clamp(y, 4, Math.Max(4, bounds.Bottom - h - 4));
        var rect = new Rect(x, y, w, h);

        dc.DrawRectangle(Palette.Brush(Color.Parse("#1B2747"), 0.97), new Pen(Palette.Brush(color, 0.9), 1.4), rect, 9, 9);
        var tailX = x > pill.X ? rect.X : rect.Right;
        var tip = x > pill.X ? new Point(pill.Right + 2, pill.Center.Y) : new Point(pill.X - 2, pill.Center.Y);
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(tip, true);
            g.LineTo(new Point(tailX, pill.Center.Y - 5));
            g.LineTo(new Point(tailX, pill.Center.Y + 5));
            g.EndFigure(true);
        }

        dc.DrawGeometry(Palette.Brush(color, 0.9), null, geometry);
        dc.DrawText(text, new Point(rect.X + 9, rect.Y + 5));
    }

    private void DrawBanner(DrawingContext dc, Rect area, string banner, Tone tone)
    {
        var color = Palette.ForTone(tone);
        var text = Txt(banner, 14, Palette.Text, FontWeight.Bold, maxWidth: Math.Max(200, area.Width - 120));
        var w = text.Width + 40;
        var h = text.Height + 16;
        var rect = new Rect(area.Center.X - w / 2, area.Bottom - h - 12, w, h);
        var pulse = tone == Tone.Bad ? 0.25 + 0.15 * Math.Sin(_time * 5) : 0.18;
        dc.DrawRectangle(Palette.Brush(color, pulse), null, rect.Inflate(5), 16, 16);
        dc.DrawRectangle(Palette.Brush(Palette.Panel, 0.96), new Pen(Palette.Brush(color), 2), rect, 12, 12);
        dc.DrawEllipse(Palette.Brush(color), null, new Point(rect.X + 16, rect.Center.Y), 5, 5);
        dc.DrawText(text, new Point(rect.X + 28, rect.Y + 8));
    }

    // ---- hardware ---------------------------------------------------------------------------

    private void DrawHardware(DrawingContext dc, SceneState scene, Layout layout)
    {
        if (scene.Cores.Count > 0)
        {
            var first = layout.Cores.Values.Min(r => r.X);
            var busPen = new Pen(Palette.Brush(Color.Parse("#2E4372")), 4, lineCap: PenLineCap.Round);
            dc.DrawLine(busPen, new Point(first, layout.BusY), new Point(layout.Ram.X, layout.BusY));
            var busLabel = Txt("coherence interconnect + shared L3  (cache lines move here, 64 bytes at a time)", 10, Palette.Dim);
            dc.DrawText(busLabel, new Point(first + 6, layout.BusY + 4));
            foreach (var core in layout.Cores.Values)
            {
                dc.DrawLine(new Pen(Palette.Brush(Color.Parse("#2E4372")), 2), new Point(core.Center.X, core.Bottom), new Point(core.Center.X, layout.BusY));
            }
        }

        foreach (var core in scene.Cores)
        {
            DrawCore(dc, scene, core, layout.Cores[core.Index]);
        }

        if (scene.Memory.Count > 0 || scene.Cores.Count > 0)
        {
            DrawRam(dc, scene, layout);
        }
    }

    private void DrawCore(DrawingContext dc, SceneState scene, CoreNode core, Rect box)
    {
        var flash = FlashAmount("core:" + core.Index);
        if (flash > 0)
        {
            dc.DrawRectangle(Palette.Brush(Color.Parse("#7AA2FF"), 0.3 * flash), null, box.Inflate(3 + (1 - flash) * 6), 12, 12);
        }

        var thread = core.ThreadId is null ? null : scene.Threads.FirstOrDefault(t => t.Id == core.ThreadId);
        var border = thread is null ? Palette.PanelBorder : Palette.ForPhase(thread.Phase);
        dc.DrawRectangle(Palette.Brush(Color.Parse("#0E1628")), new Pen(Palette.Brush(border, 0.9), 1.5), box, 10, 10);

        var inner = box.Width - 20;
        dc.DrawText(Txt($"CORE {core.Index}", 10, Palette.Muted, FontWeight.Bold), new Point(box.X + 10, box.Y + 7));

        var chip = new Rect(box.X + 10, box.Y + 24, inner, 22);
        if (thread is null)
        {
            dc.DrawRectangle(null, new Pen(Palette.Brush(Palette.Dim), 1, new DashStyle([3, 3], 0)), chip, 11, 11);
            dc.DrawText(TxtLine("idle - halted / running other work", 10.5, Palette.Dim, inner - 16), new Point(chip.X + 10, chip.Y + 4));
        }
        else
        {
            var color = Palette.ForThread(thread.Color);
            dc.DrawRectangle(Palette.Brush(color, 0.3), new Pen(Palette.Brush(color), 1.2), chip, 11, 11);
            dc.DrawText(TxtLine($"{thread.Name}  ·  {Palette.Describe(thread.Phase)}", 10.5, Palette.Text, inner - 16, FontWeight.SemiBold), new Point(chip.X + 10, chip.Y + 4));
        }

        var y = chip.Bottom + 6;
        if (!string.IsNullOrEmpty(core.Register))
        {
            dc.DrawText(TxtLine("regs  " + core.Register, 11, Color.Parse("#C4B5FD"), inner, mono: true), new Point(box.X + 10, y));
            y += 18;
        }

        if (core.Cache.Count > 0)
        {
            dc.DrawText(Txt("L1d", 9, Palette.Dim, FontWeight.Bold), new Point(box.X + 10, y));
            y += 13;
            foreach (var line in core.Cache)
            {
                if (y + 17 > box.Bottom - (core.Note is null ? 4 : 22))
                {
                    break;
                }

                var mesi = Palette.ForMesi(line.State);
                var badge = new Rect(box.X + 10, y + 1, 16, 15);
                dc.DrawRectangle(Palette.Brush(mesi, 0.9), null, badge, 3, 3);
                var letter = Txt(line.State.ToString(), 10, Palette.Background, FontWeight.Bold);
                dc.DrawText(letter, new Point(badge.Center.X - letter.Width / 2, badge.Y + (badge.Height - letter.Height) / 2));
                var lineColor = line.State == 'I' ? Palette.Dim : Palette.Text;
                dc.DrawText(TxtLine($"{line.Key} = {line.Value}", 11, lineColor, inner - 24, mono: true), new Point(badge.Right + 6, y));
                y += 18;
            }
        }

        if (!string.IsNullOrEmpty(core.Note))
        {
            var note = Txt(core.Note!, 10.5, Color.Parse("#FBBF24"), maxWidth: inner);
            note.MaxLineCount = 2;
            note.Trimming = TextTrimming.CharacterEllipsis;
            dc.DrawText(note, new Point(box.X + 10, box.Bottom - note.Height - 6));
        }
    }

    private void DrawRam(DrawingContext dc, SceneState scene, Layout layout)
    {
        var ram = layout.Ram;
        dc.DrawRectangle(Palette.Brush(Color.Parse("#0E1628")), new Pen(Palette.Brush(Color.Parse("#2E4372")), 1.5), ram, 10, 10);
        dc.DrawText(Txt("MAIN MEMORY (DRAM)  ~100 ns away", 10, Palette.Muted, FontWeight.Bold), new Point(ram.X + 10, ram.Y + 8));

        foreach (var cell in scene.Memory)
        {
            var rect = layout.MemCells[cell.Key];
            if (rect.Bottom > ram.Bottom - 4)
            {
                break;
            }

            var flash = FlashAmount("ram:" + cell.Key);
            var tone = cell.Tone == Tone.Neutral ? Color.Parse("#2E4372") : Palette.ForTone(cell.Tone);
            if (flash > 0)
            {
                dc.DrawRectangle(Palette.Brush(tone == Color.Parse("#2E4372") ? Color.Parse("#7AA2FF") : tone, 0.45 * flash), null, rect.Inflate(3), 6, 6);
            }

            dc.DrawRectangle(Palette.Brush(Palette.Panel), new Pen(Palette.Brush(tone), 1), rect, 5, 5);
            dc.DrawText(TxtLine($"{cell.Key} = {cell.Value}", 11, Palette.Text, rect.Width - 12, mono: true), new Point(rect.X + 6, rect.Y + 3));
        }
    }

    // ---- packets ----------------------------------------------------------------------------

    private void DrawTransfers(DrawingContext dc, SceneState scene, Layout layout, bool hardwareOnly)
    {
        for (var i = 0; i < scene.Transfers.Count; i++)
        {
            var transfer = scene.Transfers[i];
            if (IsHardware(transfer.From) != hardwareOnly || IsHardware(transfer.To) != hardwareOnly)
            {
                continue;
            }

            var path = BuildPath(transfer, layout);
            if (path is null)
            {
                continue;
            }

            var color = Palette.ForTone(transfer.Tone == Tone.Neutral ? Tone.Info : transfer.Tone);
            var pen = new Pen(Palette.Brush(color, 0.35), 1.6, new DashStyle([2, 3], 0));
            for (var s = 1; s < path.Count; s++)
            {
                dc.DrawLine(pen, path[s - 1], path[s]);
            }

            // Stagger packets so simultaneous transfers are distinguishable.
            var elapsed = _time - _transferStart - i * 0.35;
            if (elapsed < 0)
            {
                continue;
            }

            var p = elapsed / PacketSeconds % 1.0;
            var eased = p < 0.5 ? 2 * p * p : 1 - Math.Pow(-2 * p + 2, 2) / 2;
            var point = PointAlong(path, eased);
            dc.DrawEllipse(Palette.Brush(color, 0.3), null, point, 12, 12);
            dc.DrawEllipse(Palette.Brush(color), null, point, 6, 6);

            var label = Txt(transfer.Label, 10.5, Palette.Text, FontWeight.SemiBold);
            var bg = new Rect(point.X + 12, point.Y - label.Height - 6, label.Width + 10, label.Height + 4);
            dc.DrawRectangle(Palette.Brush(Palette.Background, 0.9), new Pen(Palette.Brush(color, 0.8), 1), bg, 5, 5);
            dc.DrawText(label, new Point(bg.X + 5, bg.Y + 2));
        }
    }

    private static bool IsHardware(string endpoint) =>
        endpoint.StartsWith("core:", StringComparison.Ordinal) || endpoint.StartsWith("ram:", StringComparison.Ordinal);

    private static List<Point>? BuildPath(Transfer transfer, Layout layout)
    {
        var fromHardware = IsHardware(transfer.From);
        var toHardware = IsHardware(transfer.To);

        if (fromHardware && toHardware)
        {
            var a = HardwareRoute(transfer.From, layout);
            var b = HardwareRoute(transfer.To, layout);
            if (a is null || b is null)
            {
                return null;
            }

            b.Reverse();
            return [.. a, .. b];
        }

        var from = Anchor(transfer.From, layout, upward: true);
        var to = Anchor(transfer.To, layout, upward: true);
        return from is { } f && to is { } t ? [f, t] : null;
    }

    /// <summary>From an endpoint down to the bus.</summary>
    private static List<Point>? HardwareRoute(string endpoint, Layout layout)
    {
        if (endpoint.StartsWith("core:", StringComparison.Ordinal)
            && int.TryParse(endpoint.AsSpan(5), out var index)
            && layout.Cores.TryGetValue(index, out var core))
        {
            return [new Point(core.Center.X, core.Bottom), new Point(core.Center.X, layout.BusY)];
        }

        if (endpoint.StartsWith("ram:", StringComparison.Ordinal))
        {
            var key = endpoint[4..];
            var y = layout.MemCells.TryGetValue(key, out var cell) ? cell.Center.Y : layout.Ram.Center.Y;
            var x = layout.MemCells.TryGetValue(key, out var c) ? c.X : layout.Ram.X;
            return [new Point(x, y), new Point(layout.Ram.X - 14, y), new Point(layout.Ram.X - 14, layout.BusY)];
        }

        return null;
    }

    private static Point? Anchor(string endpoint, Layout layout, bool upward)
    {
        var colon = endpoint.IndexOf(':');
        if (colon < 0)
        {
            return null;
        }

        var kind = endpoint[..colon];
        var key = endpoint[(colon + 1)..];
        return kind switch
        {
            "res" when layout.Resources.TryGetValue(key, out var r) => r.Center,
            "shape" when layout.Shapes.TryGetValue(key, out var s) => s.Center,
            "thread" when layout.Threads.TryGetValue(key, out var t) => t.Center,
            "core" when int.TryParse(key, out var i) && layout.Cores.TryGetValue(i, out var c) => upward ? new Point(c.Center.X, c.Y) : c.Center,
            "ram" when layout.MemCells.TryGetValue(key, out var m) => new Point(m.X, m.Center.Y),
            "ram" => layout.Ram.Center,
            _ => null
        };
    }

    private static Point PointAlong(List<Point> path, double t)
    {
        var lengths = new double[path.Count - 1];
        var total = 0.0;
        for (var i = 1; i < path.Count; i++)
        {
            var d = path[i] - path[i - 1];
            lengths[i - 1] = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            total += lengths[i - 1];
        }

        if (total <= 0)
        {
            return path[0];
        }

        var target = t * total;
        for (var i = 0; i < lengths.Length; i++)
        {
            if (target <= lengths[i] || i == lengths.Length - 1)
            {
                var f = lengths[i] <= 0 ? 0 : Math.Clamp(target / lengths[i], 0, 1);
                return path[i] + (path[i + 1] - path[i]) * f;
            }

            target -= lengths[i];
        }

        return path[^1];
    }
}
