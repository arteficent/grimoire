using grimoire.Abstractions;

namespace grimoire.Infrastructure;

public sealed class ScenarioContext : IScenarioContext
{
    public ScenarioContext(IOutput output, IDeadlockWatchdog watchdog)
    {
        Output = output;
        Watchdog = watchdog;
    }

    public IOutput Output { get; }

    public IDeadlockWatchdog Watchdog { get; }
}
