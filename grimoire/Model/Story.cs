using grimoire.Abstractions;

namespace grimoire.Model;

public sealed record StoryStep(
    string Title,
    string Narrative,
    string Hardware,
    string Code,
    IReadOnlyList<int> Lines,
    SceneState Scene);

public sealed record Story(string Id, string Takeaway, IReadOnlyList<StoryStep> Steps);

public sealed record Lesson(string Id, string Title, string Summary, string Section, Story Story, IScenario? Live = null);

public sealed record Track(string Id, string Title, string Tagline, string Accent, string Monogram, IReadOnlyList<Lesson> Lessons);

/// <summary>Fluent builder: each step clones the previous scene and applies a mutation.</summary>
public sealed class StoryBuilder
{
    private readonly string _id;
    private readonly string _takeaway;
    private readonly List<StoryStep> _steps = [];
    private SceneState _scene = new();
    private string _code = string.Empty;

    public StoryBuilder(string id, string takeaway)
    {
        _id = id;
        _takeaway = takeaway;
    }

    public SceneState Current => _scene;

    public StoryBuilder Setup(Action<SceneState> setup)
    {
        setup(_scene);
        return this;
    }

    public StoryBuilder Code(string code)
    {
        _code = code.Replace("\r\n", "\n");
        return this;
    }

    public StoryBuilder NoCode()
    {
        _code = string.Empty;
        return this;
    }

    public StoryBuilder Step(string title, string narrative, string deeper, Action<SceneState>? change = null) =>
        Step(title, narrative, deeper, [], change);

    public StoryBuilder Step(string title, string narrative, string hardware, int[] lines, Action<SceneState>? change = null)
    {
        var next = _scene.Clone();
        next.ClearTransient();
        change?.Invoke(next);
        _scene = next;
        _steps.Add(new StoryStep(title, narrative, hardware, _code, lines, next.Clone()));
        return this;
    }

    public Story Build()
    {
        ApplyDiagramFrame();
        return new(_id, _takeaway, _steps);
    }

    private void ApplyDiagramFrame()
    {
        var shapes = _steps.SelectMany(s => s.Scene.Shapes).ToList();
        if (shapes.Count == 0)
        {
            return;
        }

        var left = shapes.Min(s => s.X);
        var top = shapes.Min(s => s.Y - (string.IsNullOrEmpty(s.Tag) ? 0 : 16));
        var right = shapes.Max(s => s.X + (s.Kind == ShapeKind.Label ? Math.Min(s.W, (s.Text?.Length ?? 0) * s.FontSize * 0.56) : s.W));
        var bottom = shapes.Max(s => s.Y + s.H + (s.Kind == ShapeKind.Circle && !string.IsNullOrEmpty(s.Sub) ? 20 : 0));
        if (_steps.Any(s => !string.IsNullOrEmpty(s.Scene.Banner)))
        {
            bottom += 64;
        }

        const double margin = 24;
        left = Math.Max(0, left - margin);
        top = Math.Max(0, top - margin);
        right = Math.Min(1000, right + margin);
        bottom = Math.Min(620, bottom + margin);
        foreach (var step in _steps)
        {
            step.Scene.Frame = (left, top, right - left, bottom - top);
        }
    }

    public Lesson Lesson(string title, string section, string summary) => new(_id, title, summary, section, Build());
}
