using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Deadlocks;

/// <summary>Fix #2 for AB-BA: timed acquisition plus randomised back-off (breaks hold-and-wait).</summary>
public sealed class TryEnterBackoffScenario : ScenarioBase
{
    public override string Id => "D3";

    public override string Title => "Fix by Monitor.TryEnter + back-off (breaks hold-and-wait)";

    public override string Description =>
        "Each thread takes the first lock, then tries the second with a timeout. On failure it releases everything, sleeps a random interval and retries.";

    public override ScenarioCategory Category => ScenarioCategory.Deadlock;

    protected override string Takeaway =>
        "Timed acquisition converts a hard deadlock into livelock-prone contention; randomised back-off plus a retry cap keeps it live. Prefer D2 when a stable order exists.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        var lockA = new object();
        var lockB = new object();
        var completed = 0;

        void Work(string who, object first, object second)
        {
            var random = new Random(who.GetHashCode());

            for (var attempt = 1; attempt <= 20; attempt++)
            {
                var tookFirst = false;
                var tookSecond = false;
                try
                {
                    Monitor.TryEnter(first, TimeSpan.FromMilliseconds(100), ref tookFirst);
                    if (tookFirst)
                    {
                        Thread.Sleep(30);
                        Monitor.TryEnter(second, TimeSpan.FromMilliseconds(100), ref tookSecond);
                    }

                    if (tookFirst && tookSecond)
                    {
                        context.Output.Step($"{who} acquired both locks on attempt {attempt}");
                        Interlocked.Increment(ref completed);
                        return;
                    }

                    context.Output.Step($"{who} backing off after attempt {attempt}");
                }
                finally
                {
                    if (tookSecond) Monitor.Exit(second);
                    if (tookFirst) Monitor.Exit(first);
                }

                Thread.Sleep(random.Next(20, 120));
            }

            context.Output.Warn($"{who} gave up after 20 attempts (starvation, not deadlock)");
        }

        context.Watchdog.Observe("tryenter-backoff", _ =>
        {
            var t1 = ThreadHelper.StartBackground("worker-1", () => Work("worker-1", lockA, lockB));
            var t2 = ThreadHelper.StartBackground("worker-2", () => Work("worker-2", lockB, lockA));
            ThreadHelper.JoinAll(TimeSpan.FromSeconds(10), t1, t2);
        }, TimeSpan.FromSeconds(6));

        context.Output.Info($"Workers that finished their critical section: {Volatile.Read(ref completed)}/2");
        return Task.CompletedTask;
    }
}
