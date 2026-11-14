namespace grimoire.Abstractions;

public interface IScenarioCatalog
{
    IReadOnlyList<IScenario> All { get; }

    IEnumerable<IGrouping<ScenarioCategory, IScenario>> ByCategory();

    IScenario? Find(string id);
}
