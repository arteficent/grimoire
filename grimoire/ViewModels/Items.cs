using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using grimoire.Model;
using grimoire.Rendering;

namespace grimoire.ViewModels;

public sealed partial class TrackCard : ObservableObject
{
    private readonly Color _baseAccent;

    public TrackCard(Track track)
    {
        Track = track;
        _baseAccent = Color.Parse(track.Accent);
        Reink();
        Lessons = track.Lessons.Select((l, i) => new LessonCard(l, this, i)).ToList();
        Sections = Lessons.GroupBy(l => l.Lesson.Section).Select(g => new SectionGroup(g.Key, g.ToList())).ToList();
    }

    public Track Track { get; }
    public string Title => Track.Title;
    public string Tagline => Track.Tagline;
    public string Monogram => Track.Monogram;

    [ObservableProperty]
    private IBrush _accent = Brushes.Transparent;

    [ObservableProperty]
    private IBrush _accentWash = Brushes.Transparent;

    public IReadOnlyList<LessonCard> Lessons { get; }
    public IReadOnlyList<SectionGroup> Sections { get; }
    public string CountLabel => $"{Lessons.Count} lessons";
    public string SectionsLabel => string.Join("  ·  ", Sections.Select(s => s.Name));

    /// <summary>Re-inks the track colour for the current paper.</summary>
    public void Reink()
    {
        var ink = Palette.InkFor(_baseAccent);
        Accent = new ImmutableSolidColorBrush(ink);
        AccentWash = new ImmutableSolidColorBrush(Palette.WithAlpha(ink, 0.14));
    }
}

public sealed record SectionGroup(string Name, IReadOnlyList<LessonCard> Lessons);

public sealed partial class LessonCard : ObservableObject
{
    public LessonCard(Lesson lesson, TrackCard track, int index)
    {
        Lesson = lesson;
        Track = track;
        Index = index;
    }

    public Lesson Lesson { get; }
    public TrackCard Track { get; }
    public int Index { get; }
    public string Id => Lesson.Id;
    public string Title => Lesson.Title;
    public string Summary => Lesson.Summary;
    public bool HasLive => Lesson.Live is not null;
    public string StepsLabel => $"{Lesson.Story.Steps.Count} steps";

    [ObservableProperty]
    private bool _isDone;
}

public sealed partial class CodeLine : ObservableObject
{
    public CodeLine(int number, string text)
    {
        Number = number;
        Text = text;
    }

    public int Number { get; }
    public string Text { get; }

    [ObservableProperty]
    private bool _isActive;
}
