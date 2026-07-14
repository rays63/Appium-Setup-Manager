namespace AppiumSetupManager.Core.Platform;

public interface IPlatformAdapter
{
    bool IsMacOs { get; }
    bool IsWindows { get; }
    bool IsLinux { get; }

    string HomeDirectory { get; }
    string DefaultAndroidSdkPath { get; }
    string DefaultJdkSearchPath { get; }

    string PackageManagerInstallCommand(string packageName);
    string? LocateExecutable(string name);
}
