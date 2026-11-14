using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Deadlocks;

/// <summary>Waiting without re-checking the predicate: the signal arrives before the wait.</summary>
public sealed class MissedPulseDeadlockScenario : ScenarioBase
{
    public override string Id => "D7";

    public override string Title => "Lost wake-up (Monitor.Wait without a predicate loop)";

    public override string Description =>
        "Monitor.Pulse is not sticky. If the producer signals before the consumer calls Wait, the signal is gone and the consumer blocks forever.";

    public override ScenarioCategory Category => ScenarioCategory.Deadlock;

    protected override string Takeaway =>
        "Always wait in a loop over the condition: while (!predicate) Monitor.Wait(gate). This also absorbs spurious wake-ups and multi-consumer races.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        context.Output.Step("Buggy version: 'if' + a single Wait");
        RunHandshake(context, usePredicateLoop: false);

        context.Output.Blank();
        context.Output.Step("Correct version: 'while' predicate loop");
        RunHandshake(context, usePredicateLoop: true);

        return Task.CompletedTask;
    }

    private static void RunHandshake(IScenarioContext context, bool usePredicateLoop)
    {
        var gate = new object();
        var hasData = false;

        context.Watchdog.Observe(usePredicateLoop ? "predicate-loop" : "lost-pulse", _ =>
        {
            // Producer runs first on purpose so the pulse is emitted with nobody waiting.
            var producer = ThreadHelper.StartBackground("producer", () =>
            {
                lock (gate)
                {
                    hasData = true;
                    Monitor.Pulse(gate);
                    context.Output.Step("producer pulsed (no waiter yet)");
                }
            });

            producer.Join();
            Thread.Sleep(100);

            var consumer = ThreadHelper.StartBackground("consumer", () =>
            {
                lock (gate)
                {
                    if (usePredicateLoop)
                    {
                        while (!hasData)
                        {
                            Monitor.Wait(gate);
                        }
                    }
                    else
                    {
                        Monitor.Wait(gate);
                    }

                    context.Output.Step("consumer observed the data");
                }
            });

            consumer.Join();
        }, TimeSpan.FromSeconds(2));
    }
}
