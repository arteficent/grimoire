using grimoire.Abstractions;

namespace grimoire.Scenarios.Synchronization;

/// <summary>An async mutex: SemaphoreSlim.WaitAsync never blocks a thread and takes a token.</summary>
public sealed class AsyncMutexScenario : ScenarioBase
{
    public override string Id => "S5";

    public override string Title => "Async-friendly mutual exclusion (SemaphoreSlim.WaitAsync)";

    public override string Description =>
        "You cannot await inside a lock block. SemaphoreSlim.WaitAsync gives mutual exclusion without blocking a thread, and supports timeout/cancellation.";

    public override ScenarioCategory Category => ScenarioCategory.Synchronization;

    protected override string Takeaway =>
        "An async lock has no thread affinity: it may be released on a different thread than it was taken on. That also means it is not reentrant - see D6.";

    protected override async Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(1, 1);
        var concurrent = 0;
        var peak = 0;

        async Task WorkAsync(int id)
        {
            if (!await gate.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false))
            {
                context.Output.Warn($"worker {id} timed out waiting for the async lock (no deadlock, just back-pressure)");
                return;
            }

            try
            {
                var current = Interlocked.Increment(ref concurrent);
                InterlockedMax(ref peak, current);
                context.Output.Step($"worker {id} entered on thread {Environment.CurrentManagedThreadId}");
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                context.Output.Step($"worker {id} leaving on thread {Environment.CurrentManagedThreadId}");
                Interlocked.Decrement(ref concurrent);
            }
            finally
            {
                gate.Release();
            }
        }

        await Task.WhenAll(Enumerable.Range(1, 5).Select(WorkAsync)).ConfigureAwait(false);

        var observedPeak = Volatile.Read(ref peak);
        if (observedPeak == 1)
        {
            context.Output.Success("Peak concurrency inside the critical section was 1 - mutual exclusion held across awaits.");
        }
        else
        {
            context.Output.Error($"Peak concurrency was {observedPeak} - mutual exclusion was violated.");
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int snapshot;
        do
        {
            snapshot = Volatile.Read(ref target);
            if (value <= snapshot)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, value, snapshot) != snapshot);
    }
}
