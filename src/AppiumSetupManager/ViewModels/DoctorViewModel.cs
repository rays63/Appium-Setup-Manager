using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;
using Avalonia.Threading;

namespace AppiumSetupManager.ViewModels;

public partial class DoctorViewModel : ObservableObject, IDisposable
{
    private readonly IDoctorService _doctor;
    private CancellationTokenSource _cts = new();
    private readonly Dictionary<string, DoctorCheckViewModel> _checkMap = new();

    public ObservableCollection<DoctorCheckViewModel> Checks { get; } = new();

    [ObservableProperty] private bool   _isRunning;
    [ObservableProperty] private string _summaryText = string.Empty;

    // ── Hero card ─────────────────────────────────────────────────────────────
    [ObservableProperty] private double _healthScorePercent;
    [ObservableProperty] private string _heroHeadline = string.Empty;
    [ObservableProperty] private string _heroDescription = string.Empty;

    private bool CanRerun() => !IsRunning;

    // Invalidate all Fix button CanExecute guards whenever IsRunning flips.
    partial void OnIsRunningChanged(bool value)
    {
        foreach (var check in Checks)
            (check.FixCommand as CommunityToolkit.Mvvm.Input.IRelayCommand)?.NotifyCanExecuteChanged();
    }

    public DoctorViewModel(IDoctorService doctor)
    {
        _doctor = doctor;
        _ = RunChecksAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRerun))]
    private async Task RunChecksAsync()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsRunning = true;
        RunChecksCommand.NotifyCanExecuteChanged();
        SummaryText = Strings.DoctorRunning;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Checks.Clear();
            _checkMap.Clear();
        });

        try
        {
            var results = await _doctor.RunChecksAsync(ct);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var check in results)
                {
                    // Capture for lambda
                    var captured = check;
                    var vm = new DoctorCheckViewModel(captured, new RelayCommand(
                        () => _ = FixCheckAsync(captured.Name),
                        () => !IsRunning));
                    _checkMap[check.Name] = vm;
                    Checks.Add(vm);
                }
                UpdateSummary();
            });
        }
        catch (OperationCanceledException)
        {
            SummaryText = string.Empty;
        }
        catch (Exception)
        {
            SummaryText = "Check failed";
        }
        finally
        {
            IsRunning = false;
            RunChecksCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task FixCheckAsync(string checkName)
    {
        if (!_checkMap.TryGetValue(checkName, out var vm)) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            vm.IsFixing = true;
        });

        try
        {
            var updated = await _doctor.FixAndRecheckAsync(checkName, _cts.Token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                vm.ApplyUpdate(updated);
                UpdateSummary();
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                vm.IsFixing = false;
            });
        }
    }

    private void UpdateSummary()
    {
        var passed = Checks.Count(c => c.Result == CheckResult.Pass);
        var total  = Checks.Count;
        SummaryText = string.Format(Strings.DoctorSummaryFormat, passed, total);

        HealthScorePercent = total > 0 ? 100.0 * passed / total : 0;
        HeroHeadline = string.Format(Strings.DoctorHeroHeadlineFormat, (int)Math.Round(HealthScorePercent));

        var failing = total - passed;
        HeroDescription = failing == 0
            ? Strings.DoctorHeroAllClear
            : string.Format(Strings.DoctorHeroIssuesFormat, failing);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
