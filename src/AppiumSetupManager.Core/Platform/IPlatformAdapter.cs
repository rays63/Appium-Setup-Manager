namespace AppiumSetupManager.Core.Platform;

public interface IPlatformAdapter
{
    bool IsMacOs { get; }
    bool IsWindows { get; }
    bool IsLinux { get; }

    /// <summary>Human-readable OS name for display, e.g. "macOS", "Windows", "Linux".</summary>
    string OsDisplayName { get; }

    string HomeDirectory { get; }
    string DefaultAndroidSdkPath { get; }
    string DefaultJdkSearchPath { get; }

    string PackageManagerInstallCommand(string packageName);
    string? LocateExecutable(string name);
}
