using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Deadlocks;

/// <summary>Fix #1 for AB-BA: impose a global total order on lock acquisition.</summary>
public sealed class LockOrderingFixScenario : ScenarioBase
{
    public override string Id => "D2";

    public override string Title => "Fix by lock ordering (breaks circular wait)";

    public override string Description =>
        "Both threads acquire the two locks in the same globally agreed order, derived here from a stable rank on each lock.";

    public override ScenarioCategory Category => ScenarioCategory.Deadlock;

    protected override string Takeaway =>
        "A total order over locks makes circular wait impossible. Give every lock a rank (account id, table name, object hash) and always acquire ascending.";

    private sealed record RankedLock(int Rank, string Name)
    {
        public object Gate { get; } = new();
    }

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        var lockA = new RankedLock(1, "A");
        var lockB = new RankedLock(2, "B");

        void TransferInOrder(string who, RankedLock first, RankedLock second)
        {
            var (outer, inner) = first.Rank <= second.Rank ? (first, second) : (second, first);

            lock (outer.Gate)
            {
                context.Output.Step($"{who} took {outer.Name} (rank {outer.Rank})");
                Thread.Sleep(50);
                lock (inner.Gate)
                {
                    context.Output.Step($"{who} took {inner.Name} (rank {inner.Rank}) - critical section done");
                }
            }
        }

        context.Watchdog.Observe("ordered-locks", _ =>
        {
            var t1 = ThreadHelper.StartBackground("worker-1", () => TransferInOrder("worker-1", lockA, lockB));
            var t2 = ThreadHelper.StartBackground("worker-2", () => TransferInOrder("worker-2", lockB, lockA));
            ThreadHelper.JoinAll(TimeSpan.FromSeconds(10), t1, t2);
        }, TimeSpan.FromSeconds(5));

        return Task.CompletedTask;
    }
}
