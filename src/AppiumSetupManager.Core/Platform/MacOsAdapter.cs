namespace AppiumSetupManager.Core.Platform;

public sealed class MacOsAdapter : IPlatformAdapter
{
    public bool IsMacOs => true;
    public bool IsWindows => false;
    public bool IsLinux => false;

    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string DefaultAndroidSdkPath => Path.Combine(HomeDirectory, "Library", "Android", "sdk");
    public string DefaultJdkSearchPath => "/Library/Java/JavaVirtualMachines";

    public string PackageManagerInstallCommand(string packageName) => $"brew install {packageName}";

    public string? LocateExecutable(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':');
        return paths.Select(p => Path.Combine(p, name)).FirstOrDefault(File.Exists);
    }
}
