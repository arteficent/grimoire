using grimoire.Abstractions;

namespace grimoire.Infrastructure;

public sealed class ScenarioCatalog : IScenarioCatalog
{
    private readonly Dictionary<string, IScenario> _byId;

    public ScenarioCatalog(IEnumerable<IScenario> scenarios)
    {
        All = scenarios.OrderBy(s => s.Category).ThenBy(s => s.Id, StringComparer.OrdinalIgnoreCase).ToList();
        _byId = All.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IScenario> All { get; }

    public IEnumerable<IGrouping<ScenarioCategory, IScenario>> ByCategory() => All.GroupBy(s => s.Category);

    public IScenario? Find(string id) => _byId.GetValueOrDefault(id.Trim());
}
