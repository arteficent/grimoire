using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using grimoire.Abstractions;
using grimoire.Infrastructure;
using grimoire.Model;
using grimoire.Stories;

namespace grimoire.ViewModels;

public enum Page
{
    Home,
    Track,
    Lesson
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _playTimer;
    private readonly ScenarioRunner _runner;
    private readonly List<LessonCard> _allLessons;
    private CancellationTokenSource? _runCts;

    public MainViewModel()
    {
        IOutput output = new UiOutput(LogLines);
        _runner = new ScenarioRunner(new ScenarioContext(output, new DeadlockWatchdog(output)));

        Tracks = Curriculum.Tracks.Select(t => new TrackCard(t)).ToList();
        _allLessons = Tracks.SelectMany(t => t.Lessons).ToList();

        _playTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(PlaySeconds) };
        _playTimer.Tick += (_, _) => AutoAdvance();
    }

    public IReadOnlyList<TrackCard> Tracks { get; }

    public ObservableCollection<LessonCard> SearchResults { get; } = [];

    public ObservableCollection<CodeLine> CodeLines { get; } = [];

    public ObservableCollection<LogLine> LogLines { get; } = [];

    public string SearchHint => $"Search {_allLessons.Count} lessons: deadlock, hash table, TLB, warp, retry...";

    public string VersionLabel => $"v{AppInfo.Version}";

    // ---- navigation -------------------------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHome), nameof(IsTrack), nameof(IsLesson))]
    private Page _page = Page.Home;

    public bool IsHome => Page == Page.Home;

    public bool IsTrack => Page == Page.Track;

    public bool IsLesson => Page == Page.Lesson;

    [ObservableProperty]
    private TrackCard? _currentTrack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLive), nameof(Takeaway), nameof(NextLesson), nameof(HasNextLesson), nameof(NextLessonLabel), nameof(LessonPosition))]
    private LessonCard? _currentLesson;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearch))]
    private string _searchText = string.Empty;

    public bool HasSearch => SearchText.Trim().Length > 0;

    public bool HasLive => CurrentLesson?.HasLive == true;

    public string Takeaway => CurrentLesson?.Lesson.Story.Takeaway ?? string.Empty;

    public LessonCard? NextLesson
    {
        get
        {
            if (CurrentLesson is null)
            {
                return null;
            }

            var lessons = CurrentLesson.Track.Lessons;
            var index = CurrentLesson.Index + 1;
            return index < lessons.Count ? lessons[index] : null;
        }
    }

    public bool HasNextLesson => NextLesson is not null;

    public string NextLessonLabel => NextLesson is { } next ? $"Next lesson: {next.Title}" : string.Empty;

    public string LessonPosition => CurrentLesson is { } l ? $"Lesson {l.Index + 1} of {l.Track.Lessons.Count}  ·  {l.Lesson.Section}" : string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        SearchResults.Clear();
        var query = value.Trim();
        if (query.Length == 0)
        {
            return;
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var lesson in _allLessons.Where(l => terms.All(t =>
                     l.Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                     || l.Summary.Contains(t, StringComparison.OrdinalIgnoreCase)
                     || l.Id.Contains(t, StringComparison.OrdinalIgnoreCase)
                     || l.Track.Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                     || l.Lesson.Section.Contains(t, StringComparison.OrdinalIgnoreCase))))
        {
            SearchResults.Add(lesson);
        }
    }

    [RelayCommand]
    private void GoHome()
    {
        StopPlaying();
        Page = Page.Home;
    }

    [RelayCommand]
    private void OpenTrack(TrackCard track)
    {
        StopPlaying();
        CurrentTrack = track;
        Page = Page.Track;
    }

    [RelayCommand]
    private void BackToTrack()
    {
        if (CurrentLesson is { } lesson)
        {
            OpenTrack(lesson.Track);
        }
        else
        {
            GoHome();
        }
    }

    [RelayCommand]
    private void OpenLesson(LessonCard lesson)
    {
        StopPlaying();
        CurrentTrack = lesson.Track;
        CurrentLesson = lesson;
        IsLiveOpen = false;
        Epoch++;
        CodeLines.Clear();
        ShowStep(0, force: true);
        Page = Page.Lesson;
    }

    [RelayCommand]
    private void OpenNextLesson()
    {
        if (NextLesson is { } next)
        {
            OpenLesson(next);
        }
    }

    public void GoBack()
    {
        switch (Page)
        {
            case Page.Lesson:
                BackToTrack();
                break;
            case Page.Track:
                GoHome();
                break;
        }
    }

    // ---- walkthrough --------------------------------------------------------------------------

    [ObservableProperty]
    private int _epoch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepLabel), nameof(CanGoBack), nameof(CanGoForward), nameof(IsLastStep), nameof(Progress))]
    private int _stepIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeeper), nameof(HasCode))]
    private StoryStep? _currentStep;

    [ObservableProperty]
    private SceneState? _currentScene;

    [ObservableProperty]
    private bool _isDeeperOpen;

    [ObservableProperty]
    private bool _isCodeOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel))]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaySecondsLabel))]
    private double _playSeconds = 6;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AnimationSpeedLabel))]
    private double _animationSpeed = 1;

    private int StepCount => CurrentLesson?.Lesson.Story.Steps.Count ?? 0;

    public string StepLabel => StepCount == 0 ? string.Empty : $"Step {StepIndex + 1} of {StepCount}";

    public double Progress => StepCount <= 1 ? 100 : 100.0 * StepIndex / (StepCount - 1);

    public bool CanGoBack => StepIndex > 0;

    public bool CanGoForward => StepIndex < StepCount - 1;

    public bool IsLastStep => StepCount > 0 && StepIndex == StepCount - 1;

    public bool HasDeeper => !string.IsNullOrWhiteSpace(CurrentStep?.Hardware);

    public bool HasCode => !string.IsNullOrWhiteSpace(CurrentStep?.Code);

    public string PlayLabel => IsPlaying ? "Pause" : "Auto-play";

    public string PlaySecondsLabel => $"Auto-play: {PlaySeconds:0} s per step";

    public string AnimationSpeedLabel => $"Animation speed: x{AnimationSpeed:0.0#}";

    private void ShowStep(int index, bool force = false)
    {
        var story = CurrentLesson?.Lesson.Story;
        if (story is null || story.Steps.Count == 0)
        {
            CurrentStep = null;
            CurrentScene = null;
            return;
        }

        index = Math.Clamp(index, 0, story.Steps.Count - 1);
        if (!force && index == StepIndex && CurrentStep is not null)
        {
            return;
        }

        StepIndex = index;
        var step = story.Steps[index];
        CurrentStep = step;
        CurrentScene = step.Scene;
        UpdateCode(step);

        // StepIndex may not change (e.g. 0 -> 0 on a new lesson), so refresh derived state explicitly.
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(IsLastStep));

        if (IsLastStep && CurrentLesson is { } lesson)
        {
            lesson.IsDone = true;
        }
    }

    private void UpdateCode(StoryStep step)
    {
        var lines = string.IsNullOrEmpty(step.Code) ? [] : step.Code.Split('\n');
        if (!CodeLines.Select(l => l.Text).SequenceEqual(lines))
        {
            CodeLines.Clear();
            for (var i = 0; i < lines.Length; i++)
            {
                CodeLines.Add(new CodeLine(i + 1, lines[i]));
            }
        }

        foreach (var line in CodeLines)
        {
            line.IsActive = step.Lines.Contains(line.Number);
        }
    }

    [RelayCommand]
    private void Next()
    {
        StopPlaying();
        ShowStep(StepIndex + 1);
    }

    [RelayCommand]
    private void Previous()
    {
        StopPlaying();
        ShowStep(StepIndex - 1);
    }

    [RelayCommand]
    private void Restart()
    {
        StopPlaying();
        Epoch++;
        ShowStep(0, force: true);
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            StopPlaying();
            return;
        }

        if (!CanGoForward)
        {
            ShowStep(0, force: true);
        }

        IsPlaying = true;
        _playTimer.Interval = TimeSpan.FromSeconds(PlaySeconds);
        _playTimer.Start();
    }

    partial void OnPlaySecondsChanged(double value) => _playTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, value));

    private void AutoAdvance()
    {
        if (!CanGoForward)
        {
            StopPlaying();
            return;
        }

        ShowStep(StepIndex + 1);
        if (!CanGoForward)
        {
            StopPlaying();
        }
    }

    private void StopPlaying()
    {
        _playTimer.Stop();
        IsPlaying = false;
    }

    // ---- live run -----------------------------------------------------------------------------

    [ObservableProperty]
    private bool _isLiveOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    private bool _isRunning;

    [ObservableProperty]
    private string _runStatus = "Runs the real .NET scenario on background threads, timed by a deadlock watchdog.";

    public bool CanRun => !IsRunning;

    [RelayCommand]
    private void ToggleLive() => IsLiveOpen = !IsLiveOpen;

    [RelayCommand]
    private async Task RunScenarioAsync()
    {
        if (CurrentLesson?.Lesson.Live is not { } scenario || IsRunning)
        {
            return;
        }

        IsRunning = true;
        RunStatus = $"Running {scenario.Id}...";
        using var cts = new CancellationTokenSource();
        _runCts = cts;
        try
        {
            await Task.Run(() => _runner.RunAsync(scenario, cts.Token));
            RunStatus = cts.IsCancellationRequested ? $"{scenario.Id} cancelled." : $"{scenario.Id} finished.";
        }
        finally
        {
            _runCts = null;
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void CancelRun()
    {
        _runCts?.Cancel();
        RunStatus = "Cancellation requested. Deadlocked threads cannot be cancelled; the watchdog gives up on them.";
    }

    [RelayCommand]
    private void ClearLog() => LogLines.Clear();
}
