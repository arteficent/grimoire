using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using grimoire.ViewModels;

namespace grimoire.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _subscribed;

    public MainWindow()
    {
        InitializeComponent();
        // Inter's contextual alternates turn "0x1F" into "0×1F", which misreads as multiplication in hex-heavy lessons.
        FontFeatures = [FontFeature.Parse("-calt")];
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_subscribed is not null)
        {
            _subscribed.LogLines.CollectionChanged -= OnLogChanged;
        }

        _subscribed = DataContext as MainViewModel;
        if (_subscribed is not null)
        {
            _subscribed.LogLines.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _subscribed is not { } vm || vm.LogLines.Count == 0)
        {
            return;
        }

        var last = vm.LogLines[^1];
        Dispatcher.UIThread.Post(() => LogList.ScrollIntoView(last), DispatcherPriority.Background);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            vm.GoBack();
            e.Handled = true;
            return;
        }

        // Leave typing and slider adjustment alone; arrows only drive the lesson page.
        if (!vm.IsLesson || FocusManager?.GetFocusedElement() is TextBox or Slider)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Right or Key.PageDown:
                vm.NextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left or Key.PageUp:
                vm.PreviousCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Space:
                vm.TogglePlayCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Home:
                vm.RestartCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
