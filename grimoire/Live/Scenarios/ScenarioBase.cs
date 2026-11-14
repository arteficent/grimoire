using grimoire.Abstractions;

namespace grimoire.Scenarios;

/// <summary>Template method: the header/takeaway ceremony lives here, the demo lives in the subclass.</summary>
public abstract class ScenarioBase : IScenario
{
    public abstract string Id { get; }

    public abstract string Title { get; }

    public abstract string Description { get; }

    public abstract ScenarioCategory Category { get; }

    public async Task RunAsync(IScenarioContext context, CancellationToken cancellationToken)
    {
        context.Output.Title($"{Id}  {Title}");
        context.Output.Info(Description);
        context.Output.Blank();

        await ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

        context.Output.Blank();
        context.Output.Info("Takeaway: " + Takeaway);
    }

    protected abstract string Takeaway { get; }

    protected abstract Task ExecuteAsync(IScenarioContext context, CancellationToken cancellationToken);
}
