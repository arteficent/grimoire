using System.Diagnostics;
using grimoire.Abstractions;

namespace grimoire.Infrastructure;

public sealed class ScenarioRunner
{
    private readonly IScenarioContext _context;

    public ScenarioRunner(IScenarioContext context) => _context = context;

    public async Task RunAsync(IScenario scenario, CancellationToken cancellationToken)
    {
        var output = _context.Output;
        output.Blank();
        output.Info(new string('-', 90));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await scenario.RunAsync(_context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            output.Warn($"{scenario.Id} was cancelled.");
        }
        catch (Exception ex)
        {
            output.Error($"{scenario.Id} threw {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            output.Info($"{scenario.Id} finished in {stopwatch.ElapsedMilliseconds:N0} ms");
            output.Info(new string('-', 90));
        }
    }
}
