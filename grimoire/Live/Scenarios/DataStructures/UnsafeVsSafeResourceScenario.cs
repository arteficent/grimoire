using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.DataStructures;

/// <summary>The same hammering routine pointed at an unsafe and a safe resource.</summary>
public sealed class UnsafeVsSafeResourceScenario : ScenarioBase
{
    private const int Writers = 8;
    private const int ItemsPerWriter = 5_000;
    private const int Expected = Writers * ItemsPerWriter;

    public override string Id => "C1";

    public override string Title => "Thread-unsafe vs thread-safe resource (List / Dictionary / Queue)";

    public override string Description =>
        "List<T>.Add is read index, write slot, increment count. Concurrently that loses items, writes nulls into the backing array, or throws from a mid-flight resize.";

    public override ScenarioCategory Category => ScenarioCategory.DataStructure;

    protected override string Takeaway =>
        "A corrupted List<T> does not necessarily throw - silent data loss is the common outcome, which is why races survive testing. Guard the whole compound operation.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        Hammer(context, new ThreadUnsafeResource());
        context.Output.Blank();
        Hammer(context, new ThreadSafeResource());

        return Task.CompletedTask;
    }

    private static void Hammer(IScenarioContext context, ISharedResource resource)
    {
        context.Output.Info(resource.Name);
        var failures = 0;

        var threads = Enumerable.Range(0, Writers).Select(w => ThreadHelper.StartBackground($"writer-{w}", () =>
        {
            for (var i = 0; i < ItemsPerWriter; i++)
            {
                try
                {
                    resource.Add(w * ItemsPerWriter + i);
                }
                catch (Exception ex)
                {
                    // Torn internal state surfaces as IndexOutOfRange / NullReference / InvalidOperation.
                    Interlocked.Increment(ref failures);
                    if (Volatile.Read(ref failures) == 1)
                    {
                        context.Output.Warn($"first exception from a racing writer: {ex.GetType().Name}");
                    }
                }
            }
        })).ToArray();

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(30), threads);

        var count = resource.Count;
        var message = $"count = {count:N0} of {Expected:N0} expected, exceptions = {Volatile.Read(ref failures):N0}";

        if (count == Expected && failures == 0)
        {
            context.Output.Success(message);
        }
        else
        {
            context.Output.Error(message);
        }
    }
}
