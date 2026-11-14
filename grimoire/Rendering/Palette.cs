using Avalonia.Media;
using Avalonia.Media.Immutable;
using grimoire.Model;

namespace grimoire.Rendering;

internal static class Palette
{
    private static readonly Dictionary<uint, IImmutableSolidColorBrush> Brushes = [];

    public static readonly Color Background = Color.Parse("#0B1020");
    public static readonly Color Panel = Color.Parse("#111A2E");
    public static readonly Color PanelBorder = Color.Parse("#1F2C4A");
    public static readonly Color Text = Color.Parse("#E6ECFF");
    public static readonly Color Muted = Color.Parse("#8A9BBE");
    public static readonly Color Dim = Color.Parse("#4A5A80");
    public static readonly Color Grid = Color.Parse("#16213A");
    public static readonly Color Hold = Color.Parse("#34D399");
    public static readonly Color Wait = Color.Parse("#F43F5E");

    private static readonly Color[] ThreadColors =
    [
        Color.Parse("#7AA2FF"),
        Color.Parse("#FF9F68"),
        Color.Parse("#6EE7B7"),
        Color.Parse("#F472B6"),
        Color.Parse("#FACC15"),
        Color.Parse("#A78BFA"),
        Color.Parse("#38BDF8"),
        Color.Parse("#FB7185")
    ];

    public static Color ForThread(int index) => ThreadColors[index % ThreadColors.Length];

    public static Color ForPhase(ThreadPhase phase) => phase switch
    {
        ThreadPhase.Running => Color.Parse("#34D399"),
        ThreadPhase.Spinning => Color.Parse("#FB923C"),
        ThreadPhase.Blocked => Color.Parse("#F43F5E"),
        ThreadPhase.Waiting => Color.Parse("#FBBF24"),
        ThreadPhase.Sleeping => Color.Parse("#60A5FA"),
        ThreadPhase.Done => Color.Parse("#94A3B8"),
        ThreadPhase.Faulted => Color.Parse("#E879F9"),
        _ => Color.Parse("#64748B")
    };

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
        Tone.Info => Color.Parse("#7AA2FF"),
        Tone.Good => Color.Parse("#34D399"),
        Tone.Warn => Color.Parse("#FBBF24"),
        Tone.Bad => Color.Parse("#F43F5E"),
        _ => Color.Parse("#2A3A60")
    };

    public static Color ForKind(ResourceKind kind) => kind switch
    {
        ResourceKind.Lock => Color.Parse("#C084FC"),
        ResourceKind.Semaphore => Color.Parse("#22D3EE"),
        ResourceKind.ReaderWriter => Color.Parse("#A3E635"),
        ResourceKind.Event => Color.Parse("#FBBF24"),
        ResourceKind.Barrier => Color.Parse("#F472B6"),
        ResourceKind.Queue => Color.Parse("#38BDF8"),
        ResourceKind.Variable => Color.Parse("#E2E8F0"),
        ResourceKind.Collection => Color.Parse("#2DD4BF"),
        ResourceKind.Pool => Color.Parse("#FB923C"),
        ResourceKind.Task => Color.Parse("#818CF8"),
        ResourceKind.Context => Color.Parse("#F97316"),
        _ => Color.Parse("#E2E8F0")
    };

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

    public static Color ForMesi(char state) => state switch
    {
        'M' => Color.Parse("#F97316"),
        'E' => Color.Parse("#34D399"),
        'S' => Color.Parse("#60A5FA"),
        'R' => Color.Parse("#C084FC"),
        _ => Color.Parse("#475569")
    };

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
}
