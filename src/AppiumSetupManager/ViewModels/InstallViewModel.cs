using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;
using Avalonia.Threading;

namespace AppiumSetupManager.ViewModels;

public enum InstallMode { Quick, Advanced }

public partial class InstallViewModel : ObservableObject, IDisposable
{
    private readonly IInstallerService _installer;

    private CancellationTokenSource _installCts = new();
    private readonly Dictionary<string, InstallStepViewModel> _stepMap = new();

    public ObservableCollection<InstallStepViewModel> Steps { get; } = new();

    [ObservableProperty]
    private InstallMode _mode = InstallMode.Quick;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private double _overallProgress;

    public bool IsAdvancedMode => Mode == InstallMode.Advanced;

    partial void OnModeChanged(InstallMode value) => OnPropertyChanged(nameof(IsAdvancedMode));

    private bool CanInstall() => !IsInstalling;
    private bool CanCancel() => IsInstalling;

    public InstallViewModel(IInstallerService installer)
    {
        _installer = installer;
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task StartInstallAsync()
    {
        _installCts.Cancel();
        _installCts = new CancellationTokenSource();
        var ct = _installCts.Token;

        IsInstalling = true;
        StartInstallCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        SummaryText = string.Empty;
        OverallProgress = 0;

        // Clear from UI thread
        Dispatcher.UIThread.Post(() =>
        {
            Steps.Clear();
            _stepMap.Clear();
        });

        try
        {
            var source = Mode == InstallMode.Quick
                ? _installer.InstallAllAsync(ct)
                : _installer.InstallSelectedAsync(GetSelectedComponents(), ct);

            await DrainAsync(source, ct);
        }
        catch (OperationCanceledException)
        {
            SummaryText = Strings.InstallCancelled;
        }
        catch (Exception)
        {
            SummaryText = Strings.InstallFailed;
        }
        finally
        {
            IsInstalling = false;
            StartInstallCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            if (string.IsNullOrEmpty(SummaryText))
                UpdateSummary();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _installCts.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task RetryAsync(string componentName)
    {
        _installCts.Cancel();
        _installCts = new CancellationTokenSource();
        var ct = _installCts.Token;

        IsInstalling = true;
        StartInstallCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();

        try
        {
            await DrainAsync(_installer.InstallSelectedAsync(new[] { componentName }, ct), ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            IsInstalling = false;
            StartInstallCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            RetryCommand.NotifyCanExecuteChanged();
            UpdateSummary();
        }
    }

    private async Task DrainAsync(IAsyncEnumerable<InstallStep> steps, CancellationToken ct)
    {
        await foreach (var step in steps.WithCancellation(ct).ConfigureAwait(false))
        {
            var captured = step;
            // InvokeAsync (not Post) — awaiting ensures the UI update is fully applied
            // before the loop advances to the next step, so UpdateSummary in the
            // finally block always reads a complete, up-to-date Steps collection.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_stepMap.TryGetValue(captured.ComponentName, out var vm))
                {
                    vm.ApplyUpdate(captured);
                }
                else
                {
                    var newVm = new InstallStepViewModel(captured, RetryCommand);
                    _stepMap[captured.ComponentName] = newVm;
                    Steps.Add(newVm);
                }
                NotifyProgressChanged();
            });
        }
    }

    private void NotifyProgressChanged()
    {
        if (Steps.Count == 0) return;
        var completed = Steps.Count(s => s.State is InstallStepState.Done or InstallStepState.Failed or InstallStepState.Skipped);
        OverallProgress = (double)completed / Steps.Count;
    }

    private void UpdateSummary()
    {
        var done    = Steps.Count(s => s.State == InstallStepState.Done);
        var total   = Steps.Count(s => s.State != InstallStepState.Skipped);
        SummaryText = string.Format(Strings.InstallSummaryFormat, done, total);
    }

    private IEnumerable<string> GetSelectedComponents() =>
        Steps.Where(s => s.IsVisible).Select(s => s.ComponentName);

    public void Dispose()
    {
        _installCts.Cancel();
        _installCts.Dispose();
    }
}
