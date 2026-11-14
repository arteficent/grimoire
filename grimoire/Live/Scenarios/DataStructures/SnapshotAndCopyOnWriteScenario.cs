using System.Collections.Concurrent;
using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.DataStructures;

/// <summary>Enumeration semantics, and the lock-free copy-on-write alternative.</summary>
public sealed class SnapshotAndCopyOnWriteScenario : ScenarioBase
{
    public override string Id => "C5";

    public override string Title => "Enumerating while writing: throw, moving snapshot, or copy-on-write";

    public override string Description =>
        "List<T>/Dictionary<K,V> throw InvalidOperationException when mutated during foreach; ConcurrentDictionary gives a moving view; copy-on-write gives a stable immutable snapshot.";

    public override ScenarioCategory Category => ScenarioCategory.DataStructure;

    protected override string Takeaway =>
        "'Thread-safe' says nothing about which consistency model you get. Copy-on-write via Interlocked.CompareExchange is ideal for read-mostly state: readers never lock and always see a coherent version.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        EnumerateWhileMutating(context);
        context.Output.Blank();
        ConcurrentDictionarySnapshot(context);
        context.Output.Blank();
        CopyOnWrite(context);

        return Task.CompletedTask;
    }

    private static void EnumerateWhileMutating(IScenarioContext context)
    {
        context.Output.Info("List<T>: foreach while another thread adds");
        var list = Enumerable.Range(0, 5_000).ToList();
        var stop = false;

        var writer = ThreadHelper.StartBackground("writer", () =>
        {
            while (!Volatile.Read(ref stop))
            {
                lock (list)
                {
                    list.Add(1);
                    if (list.Count > 20_000) list.RemoveRange(0, 10_000);
                }
            }
        });

        try
        {
            for (var attempt = 1; attempt <= 50; attempt++)
            {
                try
                {
                    var total = 0L;
                    foreach (var item in list)
                    {
                        total += item;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    context.Output.Warn($"attempt {attempt} hit the fail-fast enumerator: {ex.Message}");
                    return;
                }
                catch (ArgumentException ex)
                {
                    // A racing RemoveRange can also be observed as a torn index.
                    context.Output.Warn($"attempt {attempt} saw torn internal state: {ex.GetType().Name}");
                    return;
                }
            }

            context.Output.Info("50 enumerations happened to complete - the race is timing dependent, not absent.");
        }
        finally
        {
            Volatile.Write(ref stop, true);
            writer.Join(TimeSpan.FromSeconds(2));
        }
    }

    private static void ConcurrentDictionarySnapshot(IScenarioContext context)
    {
        context.Output.Info("ConcurrentDictionary: enumeration never throws, but it is not a point-in-time snapshot");
        var map = new ConcurrentDictionary<int, int>(Enumerable.Range(0, 1_000).Select(i => new KeyValuePair<int, int>(i, i)));
        var stop = false;

        var writer = ThreadHelper.StartBackground("writer", () =>
        {
            var next = 1_000;
            while (!Volatile.Read(ref stop))
            {
                map[next++] = 1;
            }
        });

        var first = map.Count();
        var second = map.Count();
        Volatile.Write(ref stop, true);
        writer.Join(TimeSpan.FromSeconds(2));

        context.Output.Info($"two consecutive enumerations returned {first:N0} and {second:N0} items - both valid, neither is 'the' state.");
    }

    private static void CopyOnWrite(IScenarioContext context)
    {
        context.Output.Info("Copy-on-write: readers are lock-free and always see a whole, immutable version");

        var store = new CopyOnWriteStore();
        var readerSaw = 0;
        var stop = false;

        var reader = ThreadHelper.StartBackground("reader", () =>
        {
            while (!Volatile.Read(ref stop))
            {
                var snapshot = store.Snapshot;
                var sum = 0;
                foreach (var value in snapshot)
                {
                    sum += value;
                }

                // The snapshot can never change under us, so this is always internally consistent.
                if (sum != snapshot.Length)
                {
                    Interlocked.Increment(ref readerSaw);
                }
            }
        });

        var writers = Enumerable.Range(0, 4).Select(w => ThreadHelper.StartBackground($"w{w}", () =>
        {
            for (var i = 0; i < 500; i++)
            {
                store.Add(1);
            }
        })).ToArray();

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(20), writers);
        Volatile.Write(ref stop, true);
        reader.Join(TimeSpan.FromSeconds(2));

        var message = $"final length = {store.Snapshot.Length:N0} (expected 2,000), inconsistent snapshots seen = {Volatile.Read(ref readerSaw)}";
        if (store.Snapshot.Length == 2_000 && readerSaw == 0)
        {
            context.Output.Success(message);
        }
        else
        {
            context.Output.Error(message);
        }
    }

    /// <summary>Lock-free writer via CAS retry loop; readers just take the current reference.</summary>
    private sealed class CopyOnWriteStore
    {
        private int[] _items = [];

        public int[] Snapshot => Volatile.Read(ref _items);

        public void Add(int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref _items);
                var updated = new int[current.Length + 1];
                Array.Copy(current, updated, current.Length);
                updated[^1] = value;

                if (Interlocked.CompareExchange(ref _items, updated, current) == current)
                {
                    return;
                }
                // Someone else published first: rebuild from the newer version.
            }
        }
    }
}
