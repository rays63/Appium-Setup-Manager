using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

public sealed class ComponentStatusViewModel
{
    public string         Name           { get; }
    public DetectionState State          { get; }
    public string         StatusLabel    { get; }
    public string?        VersionDisplay { get; }
    public string?        InstallPath    { get; }
    public bool           IsVisible      { get; }

    public ComponentStatusViewModel(ComponentStatus status)
    {
        Name        = status.Name;
        State       = status.State;
        InstallPath = status.InstallPath;
        IsVisible   = status.State != DetectionState.NotApplicable;

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
    }

    // Ensures the displayed version always has exactly one leading "v".
    // Node.js probes return "v20.11.0"; npm/Appium return "10.2.4". Both render as "v20.11.0" / "v10.2.4".
    private static string NormaliseVersion(string v) =>
        v.StartsWith('v') ? v : $"v{v}";
}
