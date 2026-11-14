using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using grimoire.Abstractions;

namespace grimoire.ViewModels;

public sealed record LogLine(string Text, IBrush Brush);

/// <summary>Routes the real scenarios' output into the UI log, from any thread.</summary>
public sealed class UiOutput : IOutput
{
    private const int MaxLines = 4000;

    private static readonly IBrush TitleBrush = new ImmutableSolidColorBrush(Color.Parse("#7AA2FF"));
    private static readonly IBrush InfoBrush = new ImmutableSolidColorBrush(Color.Parse("#C9D6FF"));
    private static readonly IBrush StepBrush = new ImmutableSolidColorBrush(Color.Parse("#22D3EE"));
    private static readonly IBrush SuccessBrush = new ImmutableSolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush WarnBrush = new ImmutableSolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush ErrorBrush = new ImmutableSolidColorBrush(Color.Parse("#F43F5E"));

    private readonly ObservableCollection<LogLine> _lines;

    public UiOutput(ObservableCollection<LogLine> lines) => _lines = lines;

    public void Title(string message) => Append("== " + message, TitleBrush);

    public void Info(string message) => Append("   " + message, InfoBrush);

    public void Step(string message) => Append($"-> [{Environment.CurrentManagedThreadId,3}] {message}", StepBrush);

    public void Success(string message) => Append("OK " + message, SuccessBrush);

    public void Warn(string message) => Append("!! " + message, WarnBrush);

    public void Error(string message) => Append("XX " + message, ErrorBrush);

    public void Blank() => Append(string.Empty, InfoBrush);

    private void Append(string text, IBrush brush)
    {
        var line = new LogLine(text, brush);
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
