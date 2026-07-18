using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;
using Avalonia.Threading;

namespace AppiumSetupManager.ViewModels;

public partial class InstallViewModel : ObservableObject, IDisposable
{
    private readonly IInstallerService _installer;
    private readonly IDetectionService _detection;

    private CancellationTokenSource _scanCts = new();
    private CancellationTokenSource _installCts = new();
    private readonly Dictionary<string, InstallStepViewModel> _stepMap = new();

    /// <summary>One card per catalog component, each with its own Install/Update button.</summary>
    public ObservableCollection<ComponentStatusViewModel> Components { get; } = new();

    /// <summary>Live transcript of the most recent "Install All Missing" run.</summary>
    public ObservableCollection<InstallStepViewModel> Steps { get; } = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isInstallingAll;

    [ObservableProperty]
    private bool _hasRunLog;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private double _overallProgress;

    private bool CanInstallAll() => !IsInstallingAll;
    private bool CanCancel() => IsInstallingAll;

    public event Action? InstallCompleted;

    public InstallViewModel(IInstallerService installer, IDetectionService detection)
    {
        _installer = installer;
        _detection = detection;
        _ = RefreshComponentsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanInstallAll))]
    private async Task InstallAllMissingAsync()
    {
        _installCts.Cancel();
        _installCts = new CancellationTokenSource();
        var ct = _installCts.Token;

        IsInstallingAll = true;
        HasRunLog = true;
        InstallAllMissingCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        SummaryText = string.Empty;
        OverallProgress = 0;
        SetComponentsBusy(true);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Steps.Clear();
            _stepMap.Clear();
        });

        try
        {
            await DrainAsync(_installer.InstallAllAsync(ct), ct);
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
            IsInstallingAll = false;
            InstallAllMissingCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            SetComponentsBusy(false);
            if (string.IsNullOrEmpty(SummaryText))
                UpdateSummary();

            await RefreshComponentsAsync();

            if (!ct.IsCancellationRequested && SummaryText != Strings.InstallCancelled && SummaryText != Strings.InstallFailed)
                InstallCompleted?.Invoke();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _installCts.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanInstallAll))]
    private async Task RetryAsync(string componentName)
    {
        _installCts.Cancel();
        _installCts = new CancellationTokenSource();
        var ct = _installCts.Token;

        IsInstallingAll = true;
        HasRunLog = true;
        InstallAllMissingCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        SetComponentsBusy(true);

        try
        {
            await DrainAsync(_installer.InstallSelectedAsync(new[] { componentName }, ct), ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            IsInstallingAll = false;
            InstallAllMissingCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            RetryCommand.NotifyCanExecuteChanged();
            SetComponentsBusy(false);
            UpdateSummary();
            await RefreshComponentsAsync();
        }
    }

    /// <summary>Installs a single component (per-card button), then refreshes all cards.</summary>
    private async Task InstallOneAsync(ComponentStatusViewModel component)
    {
        if (component.CatalogName is null)
            return;

        try
        {
            await foreach (var _ in _installer.InstallSelectedAsync(new[] { component.CatalogName }))
            {
                // Progress is visible via the card's own spinner; no transcript entry needed here.
            }
        }
        catch (OperationCanceledException)
        {
            // Non-fatal — fall through to a refresh so the card reflects whatever state resulted.
        }

        await RefreshComponentsAsync();
    }

    private async Task RefreshComponentsAsync()
    {
        _scanCts.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;

        try
        {
            var results = await _detection.ScanAllAsync(ct);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Components.Clear();
                foreach (var result in results)
                    Components.Add(new ComponentStatusViewModel(result, InstallOneAsync));
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            IsScanning = false;
        }
    }

    private void SetComponentsBusy(bool busy)
    {
        foreach (var component in Components)
            component.IsExternallyBusy = busy;
    }

    private async Task DrainAsync(IAsyncEnumerable<InstallStep> steps, CancellationToken ct)
    {
        await foreach (var step in steps.WithCancellation(ct).ConfigureAwait(false))
        {
            var captured = step;
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

    public void Dispose()
    {
        _scanCts.Cancel();
        _scanCts.Dispose();
        _installCts.Cancel();
        _installCts.Dispose();
    }
}
