using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;
using Avalonia.Threading;

namespace AppiumSetupManager.ViewModels;

public partial class StorageViewModel : ObservableObject, IDisposable
{
    private const double BytesPerGb = 1024.0 * 1024.0 * 1024.0;

    private readonly IStorageService _storage;
    private readonly ICleanupService _cleanup;
    private CancellationTokenSource _scanCts = new();
    private CancellationTokenSource _cleanupCts = new();

    public ObservableCollection<StorageItemViewModel> Items { get; } = new();

    /// <summary>
    /// One 0..1 fraction per distinct category's share of total reclaimable bytes — feeds the hero
    /// RingChart's multi-segment donut via FractionsToRingSegmentsConverter. Empty until a scan
    /// finds at least one item (StorageService is still a stub returning [] — see report).
    /// </summary>
    public ObservableCollection<double> CategoryFractions { get; } = new();

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private bool _isPreviewOpen;
    [ObservableProperty] private string? _toastMessage;
    [ObservableProperty] private double _totalReclaimableGb;
    [ObservableProperty] private int _categoryCount;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private double _reclaimGb;

    public bool HasItems => Items.Count > 0;
    public bool HasSelection => SelectedCount > 0;

    private bool CanScan() => !IsScanning;
    private bool CanOpenPreview() => SelectedCount > 0 && !IsCleaning;
    private bool CanConfirmCleanup() => !IsCleaning;

    public StorageViewModel(IStorageService storage, ICleanupService cleanup)
    {
        _storage = storage;
        _cleanup = cleanup;
        _ = ScanAsync();
    }

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        _scanCts.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;
        ScanCommand.NotifyCanExecuteChanged();

        try
        {
            var results = await _storage.ScanAsync(ct);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Items.Clear();
                foreach (var item in results)
                    Items.Add(new StorageItemViewModel(item, RecomputeSelection));

                RecomputeSelection();
                RecomputeCategoryBreakdown(results);
                OnPropertyChanged(nameof(HasItems));
            });
        }
        catch (OperationCanceledException)
        {
            // Non-fatal — leave whatever was scanned before cancellation in place.
        }
        catch (Exception)
        {
            // Storage scanning is best-effort presentation; a failed scan just leaves the list empty.
        }
        finally
        {
            IsScanning = false;
            ScanCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenPreview))]
    private void OpenPreview() => IsPreviewOpen = true;

    [RelayCommand]
    private void CancelPreview() => IsPreviewOpen = false;

    [RelayCommand(CanExecute = nameof(CanConfirmCleanup))]
    private async Task ConfirmCleanupAsync()
    {
        IsPreviewOpen = false;
        _cleanupCts.Cancel();
        _cleanupCts = new CancellationTokenSource();
        var ct = _cleanupCts.Token;

        IsCleaning = true;
        ConfirmCleanupCommand.NotifyCanExecuteChanged();

        try
        {
            var selected = Items.Where(i => i.IsSelected).Select(i => i.Model).ToList();
            var freedBytes = await _cleanup.DeleteAsync(selected, ct);
            ToastMessage = string.Format(Strings.StorageToastFormat, freedBytes / BytesPerGb);
        }
        catch (OperationCanceledException)
        {
            // Non-fatal — fall through to a re-scan so the list reflects whatever state resulted.
        }
        catch (Exception)
        {
            // Best-effort presentation; leave the toast unset on failure rather than fabricate a
            // success message for work that didn't actually complete.
        }
        finally
        {
            IsCleaning = false;
            ConfirmCleanupCommand.NotifyCanExecuteChanged();
        }

        await ScanAsync();
    }

    [RelayCommand]
    private void DismissToast() => ToastMessage = null;

    private void RecomputeSelection()
    {
        SelectedCount = Items.Count(i => i.IsSelected);
        ReclaimGb = Items.Where(i => i.IsSelected).Sum(i => i.SizeGb);
        OpenPreviewCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCountChanged(int value) => OnPropertyChanged(nameof(HasSelection));

    private void RecomputeCategoryBreakdown(IReadOnlyList<StorageItem> results)
    {
        var totalBytes = results.Sum(r => r.SizeBytes);
        TotalReclaimableGb = totalBytes / BytesPerGb;

        var byCategory = results
            .GroupBy(r => r.Category)
            .Select(g => g.Sum(r => r.SizeBytes))
            .ToList();
        CategoryCount = byCategory.Count;

        CategoryFractions.Clear();
        if (totalBytes > 0)
            foreach (var bytes in byCategory)
                CategoryFractions.Add((double)bytes / totalBytes);
    }

    public void Dispose()
    {
        _scanCts.Cancel();
        _scanCts.Dispose();
        _cleanupCts.Cancel();
        _cleanupCts.Dispose();
    }
}
