namespace AppiumSetupManager.Core.Models;

public enum DetectionState { NotFound, Found, Outdated }

public record ComponentStatus(
    string Name,
    DetectionState State,
    string? InstalledVersion,
    string? RequiredVersion,
    string? InstallPath,
    string? EnvVar
);
