namespace grimoire.Abstractions;

public enum WatchOutcome
{
    Completed,
    Deadlocked,
    Faulted
}

public sealed record WatchResult(WatchOutcome Outcome, TimeSpan Elapsed, Exception? Error = null)
{
    public bool IsDeadlocked => Outcome == WatchOutcome.Deadlocked;
}
