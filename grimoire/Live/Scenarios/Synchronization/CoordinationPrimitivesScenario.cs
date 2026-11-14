using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Synchronization;

/// <summary>Signalling primitives: they coordinate order, they do not protect state.</summary>
public sealed class CoordinationPrimitivesScenario : ScenarioBase
{
    public override string Id => "S3";

    public override string Title => "Coordination: ManualResetEventSlim, CountdownEvent, Barrier";

    public override string Description =>
        "Three phases of workers coordinated by a start gate, a completion latch and a phased barrier.";

    public override ScenarioCategory Category => ScenarioCategory.Synchronization;

    protected override string Takeaway =>
        "Events are sticky (Set stays set), Monitor.Pulse is not. A Barrier deadlocks if any participant leaves early - always use RemoveParticipant or SignalAndWait with a timeout.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        const int workers = 4;

        using var startGate = new ManualResetEventSlim(false);
        using var finished = new CountdownEvent(workers);
        using var barrier = new Barrier(workers, b => context.Output.Info($"-- barrier phase {b.CurrentPhaseNumber} complete --"));

        var threads = Enumerable.Range(1, workers).Select(i => ThreadHelper.StartBackground($"worker-{i}", () =>
        {
            startGate.Wait();
            context.Output.Step($"worker-{i} started");

            for (var phase = 1; phase <= 2; phase++)
            {
                Thread.Sleep(30 * i);
                context.Output.Step($"worker-{i} finished phase {phase}");
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5)))
                {
                    context.Output.Error($"worker-{i} timed out at the barrier");
                    return;
                }
            }

            finished.Signal();
        })).ToArray();

        context.Output.Info("All workers are parked on the start gate; releasing them now.");
        startGate.Set();

        if (finished.Wait(TimeSpan.FromSeconds(10)))
        {
            context.Output.Success("CountdownEvent reached zero: every worker completed both phases.");
        }
        else
        {
            context.Output.Error("CountdownEvent never reached zero.");
        }

        ThreadHelper.JoinAll(TimeSpan.FromSeconds(5), threads);
        return Task.CompletedTask;
    }
}
