using System.Collections.Concurrent;
using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.DataStructures;

/// <summary>Concurrent writes to Dictionary&lt;K,V&gt; can corrupt the bucket chain.</summary>
public sealed class DictionaryCorruptionScenario : ScenarioBase
{
    private const int Writers = 6;
    private const int ItemsPerWriter = 20_000;

    public override string Id => "C2";

    public override string Title => "Dictionary corruption vs ConcurrentDictionary";

    public override string Description =>
        "A racing resize can leave the bucket/entry arrays inconsistent. Historically that turned a lookup into an infinite loop - today it usually throws or loses entries.";

    public override ScenarioCategory Category => ScenarioCategory.DataStructure;

    protected override string Takeaway =>
        "Corruption of a hash table is unbounded: it is not just a wrong count, the instance can become permanently unusable. Never write to Dictionary<K,V> from more than one thread.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        var plain = new Dictionary<int, int>();
        var errors = 0;

        context.Watchdog.Observe("plain-dictionary", _ =>
        {
            var threads = Enumerable.Range(0, Writers).Select(w => ThreadHelper.StartBackground($"w{w}", () =>
            {
                for (var i = 0; i < ItemsPerWriter; i++)
                {
                    try
                    {
                        plain[w * ItemsPerWriter + i] = i;
                    }
                    catch (Exception ex)
                    {
                        if (Interlocked.Increment(ref errors) == 1)
                        {
                            context.Output.Warn($"first corruption symptom: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
            })).ToArray();

            ThreadHelper.JoinAll(TimeSpan.FromSeconds(20), threads);
        }, TimeSpan.FromSeconds(10));

        SafeReport(context, "Dictionary<int,int>", () => plain.Count, Writers * ItemsPerWriter, errors);

        context.Output.Blank();

        var concurrent = new ConcurrentDictionary<int, int>();
        var concurrentThreads = Enumerable.Range(0, Writers).Select(w => ThreadHelper.StartBackground($"cw{w}", () =>
        {
            for (var i = 0; i < ItemsPerWriter; i++)
            {
                concurrent[w * ItemsPerWriter + i] = i;
            }
        })).ToArray();

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(20), concurrentThreads);
        SafeReport(context, "ConcurrentDictionary<int,int>", () => concurrent.Count, Writers * ItemsPerWriter, 0);

        return Task.CompletedTask;
    }

    private static void SafeReport(IScenarioContext context, string label, Func<int> count, int expected, int errors)
    {
        int actual;
        try
        {
            actual = count();
        }
        catch (Exception ex)
        {
            context.Output.Error($"{label}: even reading Count now throws - {ex.GetType().Name}");
            return;
        }

        var message = $"{label}: {actual:N0} of {expected:N0} entries, {errors:N0} exceptions during writes";
        if (actual == expected && errors == 0)
        {
            context.Output.Success(message);
        }
        else
        {
            context.Output.Error(message);
        }
    }
}
