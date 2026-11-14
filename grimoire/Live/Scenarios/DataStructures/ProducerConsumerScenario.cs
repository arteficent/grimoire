using System.Collections.Concurrent;
using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.DataStructures;

/// <summary>Producer/consumer done three ways, including the two ways it deadlocks.</summary>
public sealed class ProducerConsumerScenario : ScenarioBase
{
    private const int Items = 2_000;

    public override string Id => "C4";

    public override string Title => "Producer/consumer: Queue vs ConcurrentQueue vs BlockingCollection";

    public override string Description =>
        "Queue<T> loses and corrupts items, ConcurrentQueue<T> forces the consumer to spin, BlockingCollection<T> blocks properly - and deadlocks if you forget CompleteAdding.";

    public override ScenarioCategory Category => ScenarioCategory.DataStructure;

    protected override string Takeaway =>
        "Bounded BlockingCollection gives back-pressure for free, but a missing CompleteAdding is a guaranteed consumer deadlock. Prefer Channel<T> when the pipeline is async.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        RaceOnPlainQueue(context);
        context.Output.Blank();
        SpinOnConcurrentQueue(context);
        context.Output.Blank();
        BlockingCollectionWithoutCompleteAdding(context);
        context.Output.Blank();
        BlockingCollectionDoneRight(context);

        return Task.CompletedTask;
    }

    private static void RaceOnPlainQueue(IScenarioContext context)
    {
        context.Output.Info("Queue<T> with two producers and one consumer");
        var queue = new Queue<int>();
        var consumed = 0;
        var errors = 0;

        var producers = Enumerable.Range(0, 2).Select(p => ThreadHelper.StartBackground($"p{p}", () =>
        {
            for (var i = 0; i < Items; i++)
            {
                try { queue.Enqueue(i); }
                catch { Interlocked.Increment(ref errors); }
            }
        })).ToArray();

        var consumer = ThreadHelper.StartBackground("consumer", () =>
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline && Volatile.Read(ref consumed) < Items * 2)
            {
                try
                {
                    if (queue.TryDequeue(out _))
                    {
                        Interlocked.Increment(ref consumed);
                    }
                }
                catch { Interlocked.Increment(ref errors); }
            }
        });

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(6), producers.Append(consumer).ToArray());

        var seen = Volatile.Read(ref consumed);
        var failures = Volatile.Read(ref errors);
        var message = $"consumed {seen:N0} of {Items * 2:N0}, {failures:N0} exceptions";

        if (seen == Items * 2 && failures == 0)
        {
            context.Output.Warn($"{message} - this run happened to survive. The race is still there; that is exactly why it reaches production.");
        }
        else
        {
            context.Output.Error($"{message} - items were lost or the internal state tore.");
        }
    }

    private static void SpinOnConcurrentQueue(IScenarioContext context)
    {
        context.Output.Info("ConcurrentQueue<T>: correct, but the consumer has to busy-wait");
        var queue = new ConcurrentQueue<int>();
        var consumed = 0;
        var producing = true;

        var spins = 0L;
        var consumer = ThreadHelper.StartBackground("c", () =>
        {
            while (Volatile.Read(ref producing) || !queue.IsEmpty)
            {
                if (queue.TryDequeue(out _))
                {
                    consumed++;
                }
                else
                {
                    spins++;
                    Thread.SpinWait(20);
                }
            }
        });

        // Started second and paced, so the consumer really does out-run the producer.
        var producer = ThreadHelper.StartBackground("p", () =>
        {
            for (var i = 0; i < Items; i++)
            {
                queue.Enqueue(i);
                if (i % 200 == 0)
                {
                    Thread.Sleep(1);
                }
            }

            Volatile.Write(ref producing, false);
        });

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(10), producer, consumer);
        context.Output.Success($"consumed {consumed:N0} of {Items:N0}, but burned {spins:N0} empty polls of CPU.");
    }

    private static void BlockingCollectionWithoutCompleteAdding(IScenarioContext context)
    {
        context.Output.Info("BlockingCollection<T> where the producer forgets CompleteAdding()");

        // Not disposed on purpose: the consumer stays parked inside GetConsumingEnumerable.
        var collection = new BlockingCollection<int>();

        context.Watchdog.Observe("missing-complete-adding", _ =>
        {
            var producer = ThreadHelper.StartBackground("p", () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    collection.Add(i);
                }
                // collection.CompleteAdding() is missing.
            });

            var consumed = 0;
            var consumer = ThreadHelper.StartBackground("c", () =>
            {
                foreach (var _ in collection.GetConsumingEnumerable())
                {
                    consumed++;
                }

                context.Output.Step($"consumer drained {consumed} items and exited");
            });

            ThreadHelper.JoinAll(TimeSpan.FromSeconds(30), producer, consumer);
        }, TimeSpan.FromSeconds(2));

        context.Output.Warn("The consumer drained everything and is still blocked: it can never know the stream ended.");
    }

    private static void BlockingCollectionDoneRight(IScenarioContext context)
    {
        context.Output.Info("BlockingCollection<T>, bounded to 16, with CompleteAdding()");

        using var collection = new BlockingCollection<int>(boundedCapacity: 16);
        var consumed = 0;

        context.Watchdog.Observe("bounded-blocking-collection", _ =>
        {
            var producer = ThreadHelper.StartBackground("p", () =>
            {
                for (var i = 0; i < Items; i++)
                {
                    collection.Add(i); // blocks when full -> back-pressure
                }

                collection.CompleteAdding();
            });

            var consumer = ThreadHelper.StartBackground("c", () =>
            {
                foreach (var _ in collection.GetConsumingEnumerable())
                {
                    consumed++;
                }
            });

            ThreadHelper.JoinAll(TimeSpan.FromSeconds(20), producer, consumer);
        }, TimeSpan.FromSeconds(10));

        context.Output.Info($"consumed {Volatile.Read(ref consumed):N0} of {Items:N0} with a bounded buffer and no polling.");
    }
}
