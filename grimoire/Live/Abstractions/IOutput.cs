namespace grimoire.Abstractions;

/// <summary>Presentation seam so scenarios never touch <see cref="System.Console"/> directly.</summary>
public interface IOutput
{
    void Title(string message);
    void Info(string message);
    void Step(string message);
    void Success(string message);
    void Warn(string message);
    void Error(string message);
    void Blank();
}
