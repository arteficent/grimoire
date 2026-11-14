using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Synchronization;

/// <summary>Read-modify-write is not atomic: the classic lost-update race.</summary>
public sealed class LostUpdateScenario : ScenarioBase
{
    private const int Threads = 8;
    private const int IterationsPerThread = 100_000;
    private const int Expected = Threads * IterationsPerThread;

    public override string Id => "S1";

    public override string Title => "Lost updates: ++ vs Interlocked vs lock";

    public override string Description =>
        "counter++ compiles to read / add / write. Without synchronisation concurrent increments overwrite each other.";

    public override ScenarioCategory Category => ScenarioCategory.Synchronization;

    protected override string Takeaway =>
        "Interlocked is the cheapest correct option for a single word; a lock is needed as soon as two or more fields must change together.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        var unsafeCounter = 0;
        Race(context, "unguarded ++", () => unsafeCounter++, () => unsafeCounter);

        var interlockedCounter = 0;
        Race(context, "Interlocked.Increment", () => Interlocked.Increment(ref interlockedCounter), () => Volatile.Read(ref interlockedCounter));

        var gate = new object();
        var lockedCounter = 0;
        Race(context, "lock + ++", () => { lock (gate) { lockedCounter++; } }, () =>
        {
            lock (gate) { return lockedCounter; }
        });

        return Task.CompletedTask;
    }

    private static void Race(IScenarioContext context, string label, Action increment, Func<int> read)
    {
        var threads = Enumerable.Range(0, Threads)
            .Select(i => ThreadHelper.StartBackground($"{label}-{i}", () =>
            {
                for (var n = 0; n < IterationsPerThread; n++)
                {
                    increment();
                }
            }))
            .ToArray();

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(30), threads);

        var actual = read();
        var message = $"{label,-22} expected {Expected:N0}, got {actual:N0} (lost {Expected - actual:N0})";

        if (actual == Expected)
        {
            context.Output.Success(message);
        }
        else
        {
            context.Output.Warn(message);
        }
    }
}
