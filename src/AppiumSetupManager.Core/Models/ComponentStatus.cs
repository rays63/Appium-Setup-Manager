namespace AppiumSetupManager.Core.Models;

public enum DetectionState
{
    NotFound = 0,
    Found = 1,
    Outdated = 2,

    /// <summary>The component is not applicable on the current platform (e.g. iOS tools on Windows/Linux).</summary>
    NotApplicable = 3,
}

public record ComponentStatus(
    string Name,
    DetectionState State,
    string? InstalledVersion,
    string? RequiredVersion,
    string? InstallPath,
    string? EnvVar
);
