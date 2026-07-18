using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// Wraps one component's <see cref="ComponentStatus"/> together with its matching InstallCatalog
/// entry and (when available) a live "latest version" resolved from the npm registry. Unlike the
/// Dashboard/Install screens' <see cref="ComponentStatusViewModel"/>, which surfaces every
/// DetectionState, this card is restricted to exactly two states — Found ("Up to date") or Outdated
/// ("Update available") — because UpdatesViewModel only ever constructs a card for a component that
/// is already installed (NotFound/NotApplicable components never make it this far).
/// </summary>
public partial class UpdateCardViewModel : ObservableObject
{
    private readonly Func<UpdateCardViewModel, Task>? _updateHandler;

    public string  Name        { get; }
    public string? CatalogName { get; }

    /// <summary>Restricted to Found ("Up to date") or Outdated ("Update available") — see class remarks.</summary>
    public DetectionState Status { get; }

    public string StatusLabel { get; }

    public string? InstalledVersionDisplay { get; }
    public string? LatestVersionDisplay { get; }

    /// <summary>
    /// "Update available: {Installed} → {Latest}" (live signal known) or
    /// "Update available: {Installed} → v{Required}+ required" (only the floor is known); null on an
    /// Up-to-date card — never fabricated marketing copy.
    /// </summary>
    public string? ReleaseNoteText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool _isUpdating;

    /// <summary>
    /// Set by UpdatesViewModel while an unrelated bulk operation ("Update All") is running, so two
    /// updates never run concurrently — mirrors ComponentStatusViewModel.IsExternallyBusy.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool _isExternallyBusy;

    [ObservableProperty]
    private string? _lastError;

    public bool CanUpdate =>
        Status == DetectionState.Outdated && CatalogName is not null && !IsUpdating && !IsExternallyBusy;

    public UpdateCardViewModel(
        ComponentStatus status,
        CatalogEntry catalogEntry,
        string? latestNpmVersion,
        Func<UpdateCardViewModel, Task>? updateHandler = null)
    {
        _updateHandler = updateHandler;

        Name        = status.Name;
        CatalogName = catalogEntry.ComponentName;

        var hasLiveSignal = catalogEntry.NpmPackageName is not null && !string.IsNullOrWhiteSpace(latestNpmVersion);

        // Precedence rule: a successful live npm check is authoritative and overrides the baseline
        // floor classification DetectionService already computed; otherwise fall back to that.
        Status = hasLiveSignal
            ? (SemVer.IsNewer(status.InstalledVersion, latestNpmVersion) ? DetectionState.Outdated : DetectionState.Found)
            : (status.State == DetectionState.Outdated ? DetectionState.Outdated : DetectionState.Found);

        StatusLabel = Status == DetectionState.Outdated ? Strings.UpdatesStatusAvailable : Strings.UpdatesStatusUpToDate;

        InstalledVersionDisplay = NormaliseVersion(status.InstalledVersion);

        if (hasLiveSignal)
        {
            LatestVersionDisplay = NormaliseVersion(latestNpmVersion);
        }
        else if (Status == DetectionState.Outdated && !string.IsNullOrWhiteSpace(status.RequiredVersion))
        {
            LatestVersionDisplay = $"v{status.RequiredVersion}+";
        }
        else
        {
            // Found with no live signal — never fabricate a distinct "latest".
            LatestVersionDisplay = InstalledVersionDisplay;
        }

        var installedForNote = InstalledVersionDisplay ?? Strings.UpdatesUnknownVersion;
        ReleaseNoteText = Status != DetectionState.Outdated
            ? null
            : hasLiveSignal
                ? string.Format(Strings.UpdatesReleaseNoteLiveFormat, installedForNote, LatestVersionDisplay)
                : string.Format(Strings.UpdatesReleaseNoteFloorFormat, installedForNote, status.RequiredVersion);
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (_updateHandler is null || !CanUpdate)
            return;

        IsUpdating = true;
        try
        {
            await _updateHandler(this);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    // Ensures the displayed version always has exactly one leading "v" — mirrors
    // ComponentStatusViewModel.NormaliseVersion.
    private static string? NormaliseVersion(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
            return null;
        return v.StartsWith('v') ? v : $"v{v}";
    }
}
