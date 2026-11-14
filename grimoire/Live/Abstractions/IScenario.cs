namespace grimoire.Abstractions;

/// <summary>
/// One self-contained, runnable demonstration. Adding a new demo means adding a new
/// implementation of this interface - no existing type has to change (open/closed).
/// </summary>
public interface IScenario
{
    string Id { get; }

    string Title { get; }

    string Description { get; }

    ScenarioCategory Category { get; }

    Task RunAsync(IScenarioContext context, CancellationToken cancellationToken);
}
