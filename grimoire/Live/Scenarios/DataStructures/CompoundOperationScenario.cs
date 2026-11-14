using System.Collections.Concurrent;
using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.DataStructures;

/// <summary>Thread-safe members do not make a multi-step sequence atomic.</summary>
public sealed class CompoundOperationScenario : ScenarioBase
{
    public override string Id => "C3";

    public override string Title => "Thread-safe type, unsafe compound operation (check-then-act)";

    public override string Description =>
        "ContainsKey followed by Add is two atomic operations, not one. And GetOrAdd may run its value factory more than once for the same key.";

    public override ScenarioCategory Category => ScenarioCategory.DataStructure;

    protected override string Takeaway =>
        "Atomicity is a property of the operation, not of the collection. Use the atomic composite APIs (GetOrAdd/AddOrUpdate/TryAdd) and wrap the factory in Lazy<T> when it must run exactly once.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        const int threads = 8;

        context.Output.Info("1) check-then-act on a ConcurrentDictionary");
        var map = new ConcurrentDictionary<string, int>();
        var duplicateAttempts = 0;

        RunAll(threads, i =>
        {
            if (!map.ContainsKey("key"))
            {
                Thread.Sleep(1);
                if (!map.TryAdd("key", i))
                {
                    Interlocked.Increment(ref duplicateAttempts);
                }
            }
        });

        context.Output.Warn($"{Volatile.Read(ref duplicateAttempts)} threads passed the ContainsKey check but lost the race to add - the 'if' told them nothing.");

        context.Output.Blank();
        context.Output.Info("2) GetOrAdd runs the value factory speculatively");

        var factoryCalls = 0;
        var expensive = new ConcurrentDictionary<string, string>();

        RunAll(threads, _ => expensive.GetOrAdd("connection", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            Thread.Sleep(20);
            return Guid.NewGuid().ToString();
        }));

        context.Output.Warn($"value factory ran {Volatile.Read(ref factoryCalls)} times; only one result was kept, the rest were built and thrown away.");

        context.Output.Blank();
        context.Output.Info("3) Lazy<T> makes the factory run exactly once");

        var lazyCalls = 0;
        var lazyMap = new ConcurrentDictionary<string, Lazy<string>>();

        RunAll(threads, _ =>
        {
            var value = lazyMap.GetOrAdd("connection", _ => new Lazy<string>(() =>
            {
                Interlocked.Increment(ref lazyCalls);
                Thread.Sleep(20);
                return Guid.NewGuid().ToString();
            }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;

            GC.KeepAlive(value);
        });

        var calls = Volatile.Read(ref lazyCalls);
        if (calls == 1)
        {
            context.Output.Success($"value factory ran {calls} time - expensive resources are created once.");
        }
        else
        {
            context.Output.Error($"value factory ran {calls} times.");
        }

        return Task.CompletedTask;
    }

    private static void RunAll(int count, Action<int> body)
    {
        var threads = Enumerable.Range(0, count)
            .Select(i => ThreadHelper.StartBackground($"t{i}", () => body(i)))
            .ToArray();

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(15), threads);
    }
}
