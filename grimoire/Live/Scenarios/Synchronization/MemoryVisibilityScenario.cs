using grimoire.Abstractions;
using grimoire.Infrastructure;

namespace grimoire.Scenarios.Synchronization;

/// <summary>Not every hang is a lock problem - some are memory-model problems.</summary>
public sealed class MemoryVisibilityScenario : ScenarioBase
{
    private bool _plainFlag;
    private volatile bool _volatileFlag;

    public override string Id => "S4";

    public override string Title => "Memory visibility: a non-volatile stop flag";

    public override string Description =>
        "A spin loop over a plain bool may never observe another thread's write: the JIT is free to hoist the read out of the loop.";

    public override ScenarioCategory Category => ScenarioCategory.Synchronization;

    protected override string Takeaway =>
        "Correctness needs a memory barrier, not just mutual exclusion. Use volatile / Volatile.Read / Interlocked / lock - and note this often only reproduces in a Release build.";

    protected override Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        _plainFlag = false;
        var plain = context.Watchdog.Observe("plain-bool-spin", _ =>
        {
            var spinner = ThreadHelper.StartBackground("spinner", () =>
            {
                while (!_plainFlag)
                {
                    // Intentionally empty: no barrier, no call, nothing to force a re-read.
                }

                context.Output.Step("plain spinner observed the flag");
            });

            Thread.Sleep(200);
            _plainFlag = true;
            spinner.Join();
        }, TimeSpan.FromSeconds(2));

        if (plain.IsDeadlocked)
        {
            context.Output.Warn("The write was never observed - a livelock caused purely by the memory model.");
        }
        else
        {
            context.Output.Info("This build happened to publish the write (Debug builds usually do). Try a Release build.");
        }

        context.Output.Blank();

        _volatileFlag = false;
        context.Watchdog.Observe("volatile-bool-spin", _ =>
        {
            var spinner = ThreadHelper.StartBackground("spinner", () =>
            {
                while (!_volatileFlag)
                {
                }

                context.Output.Step("volatile spinner observed the flag");
            });

            Thread.Sleep(200);
            _volatileFlag = true;
            spinner.Join();
        }, TimeSpan.FromSeconds(2));

        return Task.CompletedTask;
    }
}
