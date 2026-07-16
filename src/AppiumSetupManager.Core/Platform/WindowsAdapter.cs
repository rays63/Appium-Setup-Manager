namespace AppiumSetupManager.Core.Platform;

public sealed class WindowsAdapter : IPlatformAdapter
{
    public bool IsMacOs => false;
    public bool IsWindows => true;
    public bool IsLinux => false;
    public string OsDisplayName => "Windows";

    public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string DefaultAndroidSdkPath => Path.Combine(HomeDirectory, "AppData", "Local", "Android", "Sdk");
    public string DefaultJdkSearchPath => @"C:\Program Files\Eclipse Adoptium";

    public string PackageManagerInstallCommand(string packageName) => $"winget install {packageName}";

    public string? LocateExecutable(string name)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';');
        var exts = new[] { ".exe", ".cmd", ".bat" };
        return paths
            .SelectMany(p => exts.Select(e => Path.Combine(p, name + e)))
            .FirstOrDefault(File.Exists);
    }
}
