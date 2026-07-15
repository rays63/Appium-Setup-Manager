using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IDetectionService _detection;
    private CancellationTokenSource _scanCts = new();

    public ObservableCollection<ComponentStatusViewModel> Components { get; } = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _summaryText = Strings.DashboardScanning;

    public DashboardViewModel(IDetectionService detectionService)
    {
        _detection = detectionService;
        _ = RescanAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRescan))]
    private async Task RescanAsync()
    {
        _scanCts.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;
        RescanCommand.NotifyCanExecuteChanged();
        Components.Clear();

        try
        {
            var results = await _detection.ScanAllAsync(ct);
            foreach (var result in results)
                Components.Add(new ComponentStatusViewModel(result));

            var applicable = results.Where(r => r.State != DetectionState.NotApplicable).ToList();
            var found      = applicable.Count(r => r.State == DetectionState.Found);
            SummaryText    = string.Format(Strings.DashboardSummaryFormat, found, applicable.Count);
        }
        catch (OperationCanceledException)
        {
            SummaryText = Strings.DashboardScanCancelled;
        }
        catch (Exception)
        {
            SummaryText = Strings.DashboardScanFailed;
        }
        finally
        {
            IsScanning = false;
            RescanCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRescan() => !IsScanning;

    public void Dispose()
    {
        _scanCts.Cancel();
        _scanCts.Dispose();
    }
}
