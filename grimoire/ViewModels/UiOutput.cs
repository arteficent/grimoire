using System.Collections.ObjectModel;
using Avalonia.Threading;
using grimoire.Abstractions;

namespace grimoire.ViewModels;

public enum LogKind
{
    Info,
    Title,
    Step,
    Success,
    Warn,
    Error
}

public sealed record LogLine(string Text, LogKind Kind)
{
    public bool IsTitle => Kind == LogKind.Title;
    public bool IsStep => Kind == LogKind.Step;
    public bool IsSuccess => Kind == LogKind.Success;
    public bool IsWarn => Kind == LogKind.Warn;
    public bool IsError => Kind == LogKind.Error;
}

/// <summary>Routes the real scenarios' output into the UI log, from any thread.</summary>
public sealed class UiOutput : IOutput
{
    private const int MaxLines = 4000;

    private readonly ObservableCollection<LogLine> _lines;

    public UiOutput(ObservableCollection<LogLine> lines) => _lines = lines;

    public void Title(string message) => Append("== " + message, LogKind.Title);

    public void Info(string message) => Append("   " + message, LogKind.Info);

    public void Step(string message) => Append($"-> [{Environment.CurrentManagedThreadId,3}] {message}", LogKind.Step);

    public void Success(string message) => Append("OK " + message, LogKind.Success);

    public void Warn(string message) => Append("!! " + message, LogKind.Warn);

    public void Error(string message) => Append("XX " + message, LogKind.Error);

    public void Blank() => Append(string.Empty, LogKind.Info);

    private void Append(string text, LogKind kind)
    {
        var line = new LogLine(text, kind);
        Dispatcher.UIThread.Post(() =>
        {
            _lines.Add(line);
            while (_lines.Count > MaxLines)
            {
                _lines.RemoveAt(0);
            }
        });
    }
}
