namespace AppiumSetupManager.Core.Platform;

public sealed class LinuxAdapter : IPlatformAdapter
{
    public bool IsMacOs => false;
    public bool IsWindows => false;
    public bool IsLinux => true;
    public string OsDisplayName => "Linux";

    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string DefaultAndroidSdkPath => Path.Combine(HomeDirectory, "Android", "Sdk");
    public string DefaultJdkSearchPath => "/usr/lib/jvm";

    public string PackageManagerInstallCommand(string packageName) => $"sudo apt-get install -y {packageName}";

    public string? LocateExecutable(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':');
        return paths.Select(p => Path.Combine(p, name)).FirstOrDefault(File.Exists);
    }
}
