using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
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
        if (e.Action != NotifyCollectionChangedAction.Add)
            return;

        // Force layout to measure the just-added line before scrolling — ScrollToEnd() reads the
        // ScrollViewer's current Extent, which otherwise may still reflect the pre-add content size
        // (layout is normally deferred to the next pass), leaving the newest line(s) just below the
        // visible viewport during a fast burst of log output.
        LogScrollViewer.UpdateLayout();
        LogScrollViewer.ScrollToEnd();

        // Defensive follow-up at a lower priority: guarantees we land on the true bottom even if the
        // immediate UpdateLayout() above ran before the new item's container was fully realized.
        Dispatcher.UIThread.Post(() =>
        {
            LogScrollViewer.UpdateLayout();
            LogScrollViewer.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
