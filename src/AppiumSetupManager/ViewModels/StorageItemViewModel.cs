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

    /// <summary>
    /// StorageItem only carries a binary IsSafeToDelete flag — no tri-state risk level and no
    /// last-used timestamp exist anywhere in the model or StorageService. This is a 2-tier
    /// approximation (Low/Medium) derived from that flag, per the design spec; the "Last used" line
    /// called for by the mockup is omitted entirely rather than fabricated (see StorageView.axaml).
    /// </summary>
    public string RiskTag { get; }

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
        RiskTag = item.IsSafeToDelete ? Strings.StorageRiskLow : Strings.StorageRiskMedium;
        _onSelectionChanged = onSelectionChanged;
    }

    [RelayCommand]
    private void ToggleSelection() => IsSelected = !IsSelected;

    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged?.Invoke();
}
