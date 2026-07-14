using System.Collections.Specialized;
using Avalonia.Controls;
using AppiumSetupManager.ViewModels;

namespace AppiumSetupManager.Views;

public partial class CommandLogView : UserControl
{
    private CommandLogViewModel? _subscribedVm;

    public CommandLogView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.Entries.CollectionChanged -= OnEntriesChanged;
            _subscribedVm = null;
        }

        if (DataContext is CommandLogViewModel vm)
        {
            _subscribedVm = vm;
            vm.Entries.CollectionChanged += OnEntriesChanged;
        }
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            LogScrollViewer.ScrollToEnd();
    }
}
