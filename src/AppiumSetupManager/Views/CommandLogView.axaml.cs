using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AppiumSetupManager.Localization;
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
        // Entries is now a ReadOnlyObservableCollection (shared LogBuffer), which only exposes
        // CollectionChanged through the INotifyCollectionChanged interface.
        if (_subscribedVm is not null)
        {
            ((INotifyCollectionChanged)_subscribedVm.Entries).CollectionChanged -= OnEntriesChanged;
            _subscribedVm.SaveFilePickerAsync = null;
            _subscribedVm = null;
        }

        if (DataContext is CommandLogViewModel vm)
        {
            _subscribedVm = vm;
            ((INotifyCollectionChanged)vm.Entries).CollectionChanged += OnEntriesChanged;
            // The VM has no window reference — the view supplies StorageProvider access so the
            // export command can open a native save dialog without leaving MVVM.
            vm.SaveFilePickerAsync = PickSaveFileAsync;
        }
    }

    private async Task<IStorageFile?> PickSaveFileAsync(string suggestedName)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return null;

        return await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.CommandLogExportDialogTitle,
            SuggestedFileName = suggestedName,
            DefaultExtension = "txt",
            ShowOverwritePrompt = true,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(Strings.CommandLogExportFileTypeName) { Patterns = new[] { "*.txt" } },
            },
        });
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
