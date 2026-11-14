using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Deadlocks;

/// <summary>Sync-over-async: blocking on a Task whose continuation needs the blocked thread.</summary>
public sealed class SyncOverAsyncDeadlockScenario : ScenarioBase
{
    public override string Id => "D4";

    public override string Title => "Sync-over-async deadlock (.Result on a captured context)";

    public override string Description =>
        "A single-threaded SynchronizationContext (UI / classic ASP.NET) plus .Result: the continuation is queued to the very thread that is blocked waiting for it.";

    public override ScenarioCategory Category => ScenarioCategory.Deadlock;

    protected override string Takeaway =>
        "Never block on async code. If you must, use ConfigureAwait(false) in the library so the continuation runs on the pool - or better, make the call chain async all the way up.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        context.Output.Step("Case 1: awaiting WITHOUT ConfigureAwait, then blocking with .Result");
        RunOnPumpedThread(context, captureContext: true);

        context.Output.Blank();
        context.Output.Step("Case 2: same code, but the library awaits with ConfigureAwait(false)");
        RunOnPumpedThread(context, captureContext: false);

        return Task.CompletedTask;
    }

    private static void RunOnPumpedThread(IScenarioContext context, bool captureContext)
    {
        context.Watchdog.Observe(captureContext ? "result-on-captured-context" : "result-with-configureawait-false", _ =>
        {
            using var syncContext = new SingleThreadedSynchronizationContext();
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(syncContext);

            try
            {
                // The blocking call itself. On the captured-context path this never returns.
                var blocking = Task.Factory.StartNew(
                    () => LibraryCallAsync(captureContext).Result,
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext());

                syncContext.RunOnCurrentThread(blocking, TimeSpan.FromSeconds(2));

                if (blocking.IsCompleted)
                {
                    context.Output.Step("Blocking call returned normally.");
                }
                else
                {
                    context.Output.Warn("Blocking call is stuck: the pump thread waits for a continuation only it can run.");
                    Thread.Sleep(Timeout.Infinite);
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }, TimeSpan.FromSeconds(4));
    }

    private static async Task<int> LibraryCallAsync(bool captureContext)
    {
        await Task.Delay(100).ConfigureAwait(captureContext);
        return 42;
    }
}
