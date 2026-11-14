namespace grimoire.Abstractions;

/// <summary>
/// Runs a piece of work under a timeout so a genuinely deadlocked demo can be observed
/// and reported instead of hanging the process.
/// </summary>
public interface IDeadlockWatchdog
{
    WatchResult Observe(string label, Action<CancellationToken> body, TimeSpan? timeout = null);

    Task<WatchResult> ObserveAsync(string label, Func<CancellationToken, Task> body, TimeSpan? timeout = null);
}
