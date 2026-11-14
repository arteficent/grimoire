namespace grimoire.Abstractions;

/// <summary>Everything a scenario is allowed to depend on, handed to it at run time.</summary>
public interface IScenarioContext
{
    IOutput Output { get; }

    IDeadlockWatchdog Watchdog { get; }
}
