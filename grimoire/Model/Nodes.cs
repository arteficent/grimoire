namespace grimoire.Model;

public enum ThreadPhase
{
    Idle,
    Running,
    Spinning,
    Blocked,
    Waiting,
    Sleeping,
    Done,
    Faulted
}

public enum ResourceKind
{
    Lock,
    Semaphore,
    ReaderWriter,
    Event,
    Barrier,
    Queue,
    Variable,
    Collection,
    Pool,
    Task,
    Context
}

public enum Tone
{
    Neutral,
    Info,
    Good,
    Warn,
    Bad
}

public sealed class ThreadNode
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public int Color { get; init; }
    public ThreadPhase Phase { get; set; } = ThreadPhase.Idle;

    /// <summary>Resource the thread is currently operating on (drawn next to it).</summary>
    public string? At { get; set; }

    /// <summary>Speech bubble shown for one step only.</summary>
    public string? Note { get; set; }

    public ThreadNode Clone() => (ThreadNode)MemberwiseClone();
}

public sealed class ResourceNode
{
    public required string Id { get; init; }
    public required string Label { get; set; }
    public ResourceKind Kind { get; set; }

    /// <summary>Normalised position (0..1) inside the resource area.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    public string? Caption { get; set; }
    public string? Value { get; set; }
    public Tone Tone { get; set; }
    public List<string> Owners { get; private set; } = [];
    public List<string> Waiters { get; private set; } = [];
    public List<string> Items { get; private set; } = [];

    public ResourceNode Clone()
    {
        var copy = (ResourceNode)MemberwiseClone();
        copy.Owners = [.. Owners];
        copy.Waiters = [.. Waiters];
        copy.Items = [.. Items];
        return copy;
    }
}

public sealed record CacheLine(string Key, string Value, char State);

public sealed class CoreNode
{
    public required int Index { get; init; }
    public string? ThreadId { get; set; }
    public string? Register { get; set; }
    public string? Note { get; set; }
    public List<CacheLine> Cache { get; private set; } = [];

    public CoreNode Clone()
    {
        var copy = (CoreNode)MemberwiseClone();
        copy.Cache = [.. Cache];
        return copy;
    }
}

public sealed record MemCell(string Key, string Value, Tone Tone);

/// <summary>An animated packet. Endpoints: "core:0", "ram:key", "res:id", "thread:id".</summary>
public sealed record Transfer(string From, string To, string Label, Tone Tone);
