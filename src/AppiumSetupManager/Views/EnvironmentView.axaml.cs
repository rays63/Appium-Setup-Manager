using Avalonia.Controls;
using Avalonia.Platform.Storage;
using AppiumSetupManager.Localization;
using AppiumSetupManager.ViewModels;

namespace AppiumSetupManager.Views;

public partial class EnvironmentView : UserControl
{
    private EnvironmentViewModel? _subscribedVm;

    public EnvironmentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.FolderPickerAsync = null;
            _subscribedVm = null;
        }

        if (DataContext is EnvironmentViewModel vm)
        {
            _subscribedVm = vm;
            // The VM has no window reference — the view supplies StorageProvider access so the
            // Browse… command can open a native folder picker without leaving MVVM. Same
            // delegate pattern as CommandLogView.PickSaveFileAsync.
            vm.FolderPickerAsync = PickFolderAsync;
        }
    }

    private async Task<string?> PickFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.EnvironmentPathBrowseDialogTitle,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
