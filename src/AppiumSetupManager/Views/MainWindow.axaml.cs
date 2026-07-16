using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using AppiumSetupManager.ViewModels;

namespace AppiumSetupManager.Views;

public partial class MainWindow : Window
{
    private const double DefaultLogHeight = 220;

    private double _expandedLogHeight = DefaultLogHeight;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // The log panel's row is resizable via GridSplitter while expanded. When the user collapses
    // it via the header button, the row must shrink to just the header — otherwise dragging alone
    // controls height and the collapse button would leave dead space below it. Remembering the
    // last dragged height here (rather than in the ViewModel) keeps this purely a layout concern.
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.CommandLog.PropertyChanged += OnCommandLogPropertyChanged;
        ApplyLogCollapsedState(vm.CommandLog.IsCollapsed);
    }

    private void OnCommandLogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandLogViewModel.IsCollapsed) && sender is CommandLogViewModel vm)
            ApplyLogCollapsedState(vm.IsCollapsed);
    }

    private void ApplyLogCollapsedState(bool collapsed)
    {
        var logRow = RightGrid.RowDefinitions[3];

        if (collapsed)
        {
            if (logRow.Height.IsAbsolute)
                _expandedLogHeight = logRow.Height.Value;

            logRow.Height = GridLength.Auto;
        }
        else
        {
            logRow.Height = new GridLength(_expandedLogHeight, GridUnitType.Pixel);
        }

        LogSplitter.IsEnabled = !collapsed;
    }
}
