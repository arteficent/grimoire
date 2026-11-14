using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Deadlocks;

/// <summary>The textbook AB-BA deadlock: two locks taken in opposite order.</summary>
public sealed class LockOrderingDeadlockScenario : ScenarioBase
{
    public override string Id => "D1";

    public override string Title => "Lock-ordering (AB-BA) deadlock";

    public override string Description =>
        "Thread 1 takes lock A then B, thread 2 takes lock B then A. Both hold one lock and wait forever for the other.";

    public override ScenarioCategory Category => ScenarioCategory.Deadlock;

    protected override string Takeaway =>
        "All four Coffman conditions hold (mutual exclusion, hold-and-wait, no pre-emption, circular wait). Break any one of them and the deadlock disappears - see D2 and D3.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        var lockA = new object();
        var lockB = new object();
        using var bothInside = new CountdownEvent(2);

        var result = context.Watchdog.Observe("AB-BA", _ =>
        {
            var t1 = ThreadHelper.StartBackground("worker-AB", () =>
            {
                lock (lockA)
                {
                    context.Output.Step("worker-AB holds A, waiting for B...");
                    bothInside.Signal();
                    bothInside.Wait(TimeSpan.FromSeconds(2));
                    lock (lockB)
                    {
                        context.Output.Step("worker-AB holds A + B");
                    }
                }
            });

            var t2 = ThreadHelper.StartBackground("worker-BA", () =>
            {
                lock (lockB)
                {
                    context.Output.Step("worker-BA holds B, waiting for A...");
                    bothInside.Signal();
                    bothInside.Wait(TimeSpan.FromSeconds(2));
                    lock (lockA)
                    {
                        context.Output.Step("worker-BA holds B + A");
                    }
                }
            });

            ThreadHelper.JoinAll(TimeSpan.FromSeconds(30), t1, t2);
        }, TimeSpan.FromSeconds(3));

        if (result.IsDeadlocked)
        {
            context.Output.Warn("Circular wait confirmed: A -> B -> A. Neither thread can ever make progress.");
        }

        return Task.CompletedTask;
    }
}
