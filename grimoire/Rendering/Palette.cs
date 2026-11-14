using Avalonia.Media;
using Avalonia.Media.Immutable;
using grimoire.Model;

namespace grimoire.Rendering;

/// <summary>
/// Ink colours for the two paper themes: dark pens on white paper and light pens on gray paper.
/// Every renderer colour comes from here, so switching <see cref="UseDark"/> re-inks the whole scene.
/// </summary>
internal static class Palette
{
    private static readonly Dictionary<uint, IImmutableSolidColorBrush> Brushes = [];

    private static readonly Inks WhitePaper = new(
        Background: C("#FBF8F1"),
        Panel: C("#FFFDF8"),
        PanelBorder: C("#D8CFBD"),
        Text: C("#1F2330"),
        Muted: C("#5A5E6B"),
        Dim: C("#9A9384"),
        Grid: C("#E9E1D1"),
        Pencil: C("#8C8473"),
        Blue: C("#1E4FA8"),
        Green: C("#1B7347"),
        Amber: C("#A35B00"),
        Red: C("#B42318"),
        Violet: C("#6A3DB8"),
        Threads: C("#1E4FA8", "#C2410C", "#15803D", "#BE185D", "#9A6700", "#6D28D9", "#0E7490", "#B91C1C"),
        Phases: C("#15803D", "#C2410C", "#B42318", "#A35B00", "#1E4FA8", "#8A8578", "#A21CAF", "#6B6B6B"),
        Kinds: C("#7E22CE", "#0E7490", "#4D7C0F", "#A35B00", "#BE185D", "#0369A1", "#3F3F46", "#0F766E", "#C2410C", "#4338CA", "#B45309", "#3F3F46"),
        Mesi: C("#C2410C", "#15803D", "#1D4ED8", "#7E22CE", "#9A9384"));

    private static readonly Inks GrayPaper = new(
        Background: C("#393B40"),
        Panel: C("#42454B"),
        PanelBorder: C("#5E6269"),
        Text: C("#F4F0E6"),
        Muted: C("#C6C1B5"),
        Dim: C("#8F8B83"),
        Grid: C("#45484E"),
        Pencil: C("#A8A398"),
        Blue: C("#9CC2FF"),
        Green: C("#8EE3B0"),
        Amber: C("#FFD27A"),
        Red: C("#FF9A8F"),
        Violet: C("#CDB4FF"),
        Threads: C("#9CC2FF", "#FFB38A", "#8EE3B0", "#F7A6CF", "#FFE08A", "#C9B2FF", "#8EE0EA", "#FF9AA2"),
        Phases: C("#8EE3B0", "#FFB38A", "#FF9A8F", "#FFD27A", "#9CC2FF", "#A8A398", "#F0A6F5", "#A8A398"),
        Kinds: C("#D3B0FF", "#8EE0EA", "#C8EB8A", "#FFD27A", "#F7A6CF", "#9CD5FF", "#EDE8DC", "#8EE6D6", "#FFB38A", "#B5BCFF", "#FFB38A", "#EDE8DC"),
        Mesi: C("#FFB38A", "#8EE3B0", "#9CC2FF", "#D3B0FF", "#8F8B83"));

    private static Inks _ink = WhitePaper;

    public static bool IsDark { get; private set; }

    public static void UseDark(bool dark)
    {
        IsDark = dark;
        _ink = dark ? GrayPaper : WhitePaper;
    }

    /// <summary>The paper itself.</summary>
    public static Color Background => _ink.Background;

    /// <summary>A sheet laid on the paper (cards, boxes).</summary>
    public static Color Panel => _ink.Panel;

    public static Color PanelBorder => _ink.PanelBorder;

    public static Color Text => _ink.Text;

    public static Color Muted => _ink.Muted;

    public static Color Dim => _ink.Dim;

    /// <summary>Faint ruling on the page.</summary>
    public static Color Grid => _ink.Grid;

    /// <summary>Graphite for neutral outlines, buses and connectors.</summary>
    public static Color Pencil => _ink.Pencil;

    public static Color Hold => _ink.Green;

    public static Color Wait => _ink.Red;

    public static Color Info => _ink.Blue;

    public static Color Warn => _ink.Amber;

    public static Color Violet => _ink.Violet;

    public static Color ForThread(int index) => _ink.Threads[index % _ink.Threads.Length];

    public static Color ForPhase(ThreadPhase phase) => _ink.Phases[phase switch
    {
        ThreadPhase.Running => 0,
        ThreadPhase.Spinning => 1,
        ThreadPhase.Blocked => 2,
        ThreadPhase.Waiting => 3,
        ThreadPhase.Sleeping => 4,
        ThreadPhase.Done => 5,
        ThreadPhase.Faulted => 6,
        _ => 7
    }];

    public static string Describe(ThreadPhase phase) => phase switch
    {
        ThreadPhase.Running => "RUNNING",
        ThreadPhase.Spinning => "SPINNING (burning CPU)",
        ThreadPhase.Blocked => "BLOCKED (kernel wait)",
        ThreadPhase.Waiting => "WAITING (signal/timeout)",
        ThreadPhase.Sleeping => "SLEEPING",
        ThreadPhase.Done => "FINISHED",
        ThreadPhase.Faulted => "FAULTED",
        _ => "NOT STARTED"
    };

    public static Color ForTone(Tone tone) => tone switch
    {
        Tone.Info => _ink.Blue,
        Tone.Good => _ink.Green,
        Tone.Warn => _ink.Amber,
        Tone.Bad => _ink.Red,
        _ => _ink.Pencil
    };

    public static Color ForKind(ResourceKind kind) => _ink.Kinds[kind switch
    {
        ResourceKind.Lock => 0,
        ResourceKind.Semaphore => 1,
        ResourceKind.ReaderWriter => 2,
        ResourceKind.Event => 3,
        ResourceKind.Barrier => 4,
        ResourceKind.Queue => 5,
        ResourceKind.Variable => 6,
        ResourceKind.Collection => 7,
        ResourceKind.Pool => 8,
        ResourceKind.Task => 9,
        ResourceKind.Context => 10,
        _ => 11
    }];

    public static string KindLabel(ResourceKind kind) => kind switch
    {
        ResourceKind.Lock => "MONITOR LOCK",
        ResourceKind.Semaphore => "SEMAPHORE",
        ResourceKind.ReaderWriter => "READER/WRITER LOCK",
        ResourceKind.Event => "EVENT / SIGNAL",
        ResourceKind.Barrier => "BARRIER",
        ResourceKind.Queue => "QUEUE",
        ResourceKind.Variable => "SHARED MEMORY",
        ResourceKind.Collection => "COLLECTION",
        ResourceKind.Pool => "WORKER POOL",
        ResourceKind.Task => "TASK",
        ResourceKind.Context => "SYNC CONTEXT",
        _ => kind.ToString().ToUpperInvariant()
    };

    public static Color ForMesi(char state) => _ink.Mesi[state switch
    {
        'M' => 0,
        'E' => 1,
        'S' => 2,
        'R' => 3,
        _ => 4
    }];

    /// <summary>
    /// Re-inks an arbitrary colour for the current paper: a deep, readable pen on white paper and a
    /// soft pastel pen on gray paper, keeping the hue.
    /// </summary>
    public static Color InkFor(Color c)
    {
        var (h, s, _) = ToHsl(c);
        return IsDark ? FromHsl(h, Math.Min(s, 0.85), 0.78) : FromHsl(h, Math.Min(s, 0.8), 0.33);
    }

    public static Color WithAlpha(Color c, double alpha) =>
        Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B);

    public static IImmutableSolidColorBrush Brush(Color c)
    {
        if (!Brushes.TryGetValue(c.ToUInt32(), out var brush))
        {
            brush = new ImmutableSolidColorBrush(c);
            Brushes[c.ToUInt32()] = brush;
        }

        return brush;
    }

    public static IImmutableSolidColorBrush Brush(Color c, double alpha) => Brush(WithAlpha(c, alpha));

    private static (double H, double S, double L) ToHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max - min < 1e-6)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        var h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        static double Channel(double p, double q, double t)
        {
            t = t < 0 ? t + 1 : t > 1 ? t - 1 : t;
            return t < 1.0 / 6 ? p + (q - p) * 6 * t
                : t < 0.5 ? q
                : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6
                : p;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return Color.FromRgb(
            (byte)Math.Round(Channel(p, q, h + 1.0 / 3) * 255),
            (byte)Math.Round(Channel(p, q, h) * 255),
            (byte)Math.Round(Channel(p, q, h - 1.0 / 3) * 255));
    }

    private static Color C(string hex) => Color.Parse(hex);

    private static Color[] C(params string[] hex) => [.. hex.Select(Color.Parse)];

    private sealed record Inks(
        Color Background, Color Panel, Color PanelBorder, Color Text, Color Muted, Color Dim, Color Grid, Color Pencil,
        Color Blue, Color Green, Color Amber, Color Red, Color Violet,
        Color[] Threads, Color[] Phases, Color[] Kinds, Color[] Mesi);
}
