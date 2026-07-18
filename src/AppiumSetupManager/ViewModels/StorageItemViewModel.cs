using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

public partial class StorageItemViewModel : ObservableObject
{
    private const double BytesPerGb = 1024.0 * 1024.0 * 1024.0;

    private readonly Action? _onSelectionChanged;

    /// <summary>The underlying Core model — needed by StorageViewModel to build the delete request.</summary>
    public StorageItem Model { get; }

    public string Category { get; }
    public string Name { get; }
    public string Path { get; }
    public double SizeGb { get; }
    public string SizeDisplay { get; }
    public bool IsSafeToDelete { get; }

    public string RiskTag { get; }

    // One flag per risk tier so the view can keep three sibling pill Borders with their own
    // DynamicResource brushes (same pattern the previous 2-tier pills used — stays theme-reactive).
    public bool IsLowRisk { get; }
    public bool IsReviewRisk { get; }
    public bool IsInUseRisk { get; }

    /// <summary>"Last used Jun 12, 2026" (local time, date only) — null when the model has no timestamp.</summary>
    public string? LastUsedDisplay { get; }
    public bool HasLastUsed { get; }

    [ObservableProperty]
    private bool _isSelected;

    public StorageItemViewModel(StorageItem item, Action? onSelectionChanged = null)
    {
        Model = item;
        Category = item.Category;
        Name = item.Name;
        Path = item.Path;
        SizeGb = item.SizeBytes / BytesPerGb;
        SizeDisplay = string.Format(Strings.StorageSizeGbFormat, SizeGb);
        IsSafeToDelete = item.IsSafeToDelete;
        IsLowRisk = item.Risk == RiskLevel.Low;
        IsReviewRisk = item.Risk == RiskLevel.Review;
        IsInUseRisk = item.Risk == RiskLevel.InUse;
        RiskTag = item.Risk switch
        {
            RiskLevel.Low => Strings.StorageRiskLow,
            RiskLevel.Review => Strings.StorageRiskReview,
            _ => Strings.StorageRiskInUse,
        };
        HasLastUsed = item.LastUsedUtc is not null;
        LastUsedDisplay = item.LastUsedUtc is { } lastUsedUtc
            ? string.Format(Strings.StorageLastUsedFormat,
                DateTime.SpecifyKind(lastUsedUtc, DateTimeKind.Utc).ToLocalTime())
            : null;
        _onSelectionChanged = onSelectionChanged;
    }

    // Guard mirrors the CheckBox's IsEnabled gate: only Low-risk (IsSafeToDelete) items are
    // selectable — Review/InUse rows render dimmed and cannot enter the cleanup selection.
    [RelayCommand(CanExecute = nameof(IsSafeToDelete))]
    private void ToggleSelection() => IsSelected = !IsSelected;

    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged?.Invoke();
}
