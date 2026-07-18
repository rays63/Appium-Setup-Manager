using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

public partial class ComponentStatusViewModel : ObservableObject
{
    // Maps a detection component name → the InstallCatalog component name that installs it.
    // Only components with an entry here can be installed directly from the dashboard.
    private static readonly Dictionary<string, string> InstallMap = new(StringComparer.Ordinal)
    {
        ["Homebrew"]           = "Homebrew",
        ["Node.js"]            = "Node.js",
        ["JDK"]                = "JDK 21",
        ["JAVA_HOME"]          = "JDK 21",
        ["ADB"]                = "Android SDK",
        ["ANDROID_HOME"]       = "Android SDK",
        ["Appium"]             = "Appium",
        ["Appium Inspector"]   = "Appium Inspector",
        ["Xcode CLI Tools"]    = "Xcode CLI Tools",
        ["UiAutomator2 Driver"] = "UiAutomator2 Driver",
        ["XCUITest Driver"]     = "XCUITest Driver",
    };

    private readonly Func<ComponentStatusViewModel, Task>? _installHandler;

    public string         Name           { get; }
    public DetectionState State          { get; }
    public string         StatusLabel    { get; }
    public string?        VersionDisplay { get; }
    public string?        RequiredVersionDisplay { get; }
    public string?        InstallPath    { get; }
    public bool           IsVisible      { get; }

    /// <summary>The InstallCatalog name to install this component, or null if it is not installable.</summary>
    public string? CatalogName { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(CanClickInstall))]
    private bool _isInstalling;

    /// <summary>
    /// Set by a host view model to disable this item's button while an unrelated bulk operation
    /// (e.g. "Install All Missing") is running, so two installs never run concurrently.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClickInstall))]
    private bool _isExternallyBusy;

    /// <summary>True when this component is missing/outdated, installable, and not already installing.</summary>
    public bool CanInstall =>
        CatalogName is not null
        && !IsInstalling
        && State is DetectionState.NotFound or DetectionState.Outdated;

    /// <summary>
    /// True whenever this item is installable, regardless of whether it's currently running — unlike
    /// <see cref="CanInstall"/>, this stays true while installing so a host view can keep the button
    /// in place (showing a spinner) instead of it disappearing mid-install.
    /// </summary>
    public bool IsInstallable =>
        CatalogName is not null && State is DetectionState.NotFound or DetectionState.Outdated;

    /// <summary>Whether the install button should currently be clickable.</summary>
    public bool CanClickInstall => IsInstallable && !IsInstalling && !IsExternallyBusy;

    /// <summary>Button caption — "Update" for an outdated component, otherwise "Install".</summary>
    public string InstallActionLabel =>
        State == DetectionState.Outdated ? Strings.DashboardUpdate : Strings.DashboardInstall;

    public ComponentStatusViewModel(ComponentStatus status, Func<ComponentStatusViewModel, Task>? installHandler = null)
    {
        _installHandler = installHandler;

        Name        = status.Name;
        State       = status.State;
        InstallPath = status.InstallPath;
        IsVisible   = status.State != DetectionState.NotApplicable;
        CatalogName = InstallMap.TryGetValue(status.Name, out var catalog) ? catalog : null;

        StatusLabel = status.State switch
        {
            DetectionState.Found         => Strings.StatusFound,
            DetectionState.Outdated      => Strings.StatusOutdated,
            DetectionState.NotFound      => Strings.StatusNotFound,
            DetectionState.NotApplicable => Strings.StatusNotApplicable,
            _                            => status.State.ToString(),
        };

        VersionDisplay = status.State switch
        {
            DetectionState.Found =>
                status.InstalledVersion is not null
                    ? NormaliseVersion(status.InstalledVersion)
                    : null,

            DetectionState.Outdated =>
                status.InstalledVersion is not null && status.RequiredVersion is not null
                    ? $"{NormaliseVersion(status.InstalledVersion)} → needs v{status.RequiredVersion}+"
                    : status.InstalledVersion is not null
                        ? NormaliseVersion(status.InstalledVersion)
                        : null,

            _ => null,
        };

        // Tile grid caption, e.g. "v18.2 · req 18+" — only rendered when a required version is
        // actually known for this component (same field InstallMap/VersionDisplay already read).
        RequiredVersionDisplay = status.RequiredVersion is not null
            ? string.Format(Strings.DashboardRequiredVersionFormat, status.RequiredVersion)
            : null;
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (_installHandler is null || CatalogName is null || IsInstalling || IsExternallyBusy)
            return;

        IsInstalling = true;
        try
        {
            await _installHandler(this);
        }
        finally
        {
            IsInstalling = false;
        }
    }

    // Ensures the displayed version always has exactly one leading "v".
    // Node.js probes return "v20.11.0"; npm/Appium return "10.2.4". Both render as "v20.11.0" / "v10.2.4".
    private static string NormaliseVersion(string v) =>
        v.StartsWith('v') ? v : $"v{v}";
}
