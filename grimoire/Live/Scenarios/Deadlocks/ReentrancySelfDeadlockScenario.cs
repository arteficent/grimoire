using grimoire.Abstractions;

namespace grimoire.Scenarios.Deadlocks;

/// <summary>Re-entering a non-reentrant primitive on the same thread: self-deadlock.</summary>
public sealed class ReentrancySelfDeadlockScenario : ScenarioBase
{
    public override string Id => "D6";

    public override string Title => "Self-deadlock: reentrant lock vs non-reentrant SemaphoreSlim";

    public override string Description =>
        "Monitor (lock) is reentrant - the owning thread may re-acquire it. SemaphoreSlim(1,1) is not: a recursive call blocks on itself forever.";

    public override ScenarioCategory Category => ScenarioCategory.Deadlock;

    protected override string Takeaway =>
        "Know the reentrancy semantics of your primitive. Never call unknown/virtual code while holding a lock, and keep the public (locking) API separate from the private (already-locked) implementation.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        var monitorGate = new object();

        context.Watchdog.Observe("monitor-recursive", _ =>
        {
            lock (monitorGate)
            {
                context.Output.Step("outer lock taken");
                lock (monitorGate)
                {
                    context.Output.Step("inner lock taken on the same thread - Monitor is reentrant");
                }
            }
        }, TimeSpan.FromSeconds(2));

        context.Output.Blank();

        using var semaphore = new SemaphoreSlim(1, 1);
        context.Watchdog.Observe("semaphore-recursive", _ =>
        {
            semaphore.Wait();
            context.Output.Step("outer Wait() succeeded (count now 0)");
            semaphore.Wait();
            context.Output.Step("this line is never reached");
            semaphore.Release();
            semaphore.Release();
        }, TimeSpan.FromSeconds(2));

        context.Output.Blank();
        context.Output.Step("Correct shape: the public method takes the semaphore, the private core assumes it is held.");

        context.Watchdog.Observe("semaphore-non-recursive", _ =>
        {
            using var gate = new SemaphoreSlim(1, 1);

            void PublicApi()
            {
                gate.Wait();
                try
                {
                    CoreAlreadyLocked();
                }
                finally
                {
                    gate.Release();
                }
            }

            void CoreAlreadyLocked() => context.Output.Step("core executed once, lock acquired exactly once");

            PublicApi();
        }, TimeSpan.FromSeconds(2));

        return Task.CompletedTask;
    }
}
