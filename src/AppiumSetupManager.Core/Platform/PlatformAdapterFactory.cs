namespace AppiumSetupManager.Core.Platform;

public static class PlatformAdapterFactory
{
    public static IPlatformAdapter Create() => OperatingSystem.IsMacOS()  ? new MacOsAdapter()
                                                : OperatingSystem.IsWindows() ? new WindowsAdapter()
                                                : new LinuxAdapter();
}
