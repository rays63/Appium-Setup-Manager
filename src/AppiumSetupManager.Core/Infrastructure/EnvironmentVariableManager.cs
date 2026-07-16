using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Infrastructure;

public interface IEnvironmentVariableManager
{
    string? Get(string name);
    Task SetUserAsync(string name, string value, CancellationToken ct = default);
    Task AppendToPathAsync(string directory, CancellationToken ct = default);

    /// <summary>
    /// Looks for an already-installed JDK under the platform's default JDK search path and, if
    /// found, sets JAVA_HOME to point at it. Use this when JAVA_HOME is unset but a JDK may already
    /// be present (installed outside this app, e.g. via Android Studio or a manual download).
    /// </summary>
    /// <returns>The resolved JDK home directory, or null if none was found.</returns>
    Task<string?> TryConfigureJavaHomeAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets ANDROID_HOME to the platform's default Android SDK path — and appends its
    /// platform-tools directory to PATH — if a valid SDK is already present there. Use this when
    /// ANDROID_HOME is unset but the SDK may already be installed (e.g. via Android Studio).
    /// </summary>
    /// <returns>The resolved SDK directory, or null if no valid SDK was found there.</returns>
    Task<string?> TryConfigureAndroidHomeAsync(CancellationToken ct = default);
}

public sealed class EnvironmentVariableManager : IEnvironmentVariableManager
{
    private readonly IPlatformAdapter _platform;

    public EnvironmentVariableManager(IPlatformAdapter platform)
    {
        _platform = platform;
    }

    public string? Get(string name) =>
        Environment.GetEnvironmentVariable(name);

    public async Task SetUserAsync(string name, string value, CancellationToken ct = default)
    {
        // Persisted writes below (registry / shell rc file) only take effect for *new*
        // processes/shells — this app's own already-running process needs its live environment
        // updated too, or an immediate re-scan would still read the old (missing) value and never
        // show the change that was just made.
        Environment.SetEnvironmentVariable(name, value);

        if (_platform.IsWindows)
        {
            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.User);
        }
        else
        {
            var rcFile = GetShellRcFile();
            var marker = $"export {name}=";

            if (!await FileContainsAsync(rcFile, marker, ct).ConfigureAwait(false))
                await File.AppendAllTextAsync(rcFile, $"\nexport {name}=\"{value}\"\n", ct).ConfigureAwait(false);
        }
    }

    public async Task AppendToPathAsync(string directory, CancellationToken ct = default)
    {
        // Same reasoning as SetUserAsync above — update this process's live PATH immediately so a
        // freshly-installed executable (e.g. adb) can be found by this same run without restarting.
        var livePath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!livePath.Split(Path.PathSeparator).Contains(directory))
        {
            var updatedLivePath = string.IsNullOrEmpty(livePath) ? directory : $"{livePath}{Path.PathSeparator}{directory}";
            Environment.SetEnvironmentVariable("PATH", updatedLivePath);
        }

        if (_platform.IsWindows)
        {
            var current = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            if (!current.Split(Path.PathSeparator).Contains(directory))
            {
                var updated = string.IsNullOrEmpty(current)
                    ? directory
                    : $"{current}{Path.PathSeparator}{directory}";
                Environment.SetEnvironmentVariable("PATH", updated, EnvironmentVariableTarget.User);
            }
        }
        else
        {
            var rcFile = GetShellRcFile();

            if (!await FileContainsAsync(rcFile, directory, ct).ConfigureAwait(false))
                await File.AppendAllTextAsync(rcFile, $"\nexport PATH=\"$PATH:{directory}\"\n", ct).ConfigureAwait(false);
        }
    }

    public async Task<string?> TryConfigureJavaHomeAsync(CancellationToken ct = default)
    {
        var jdkHome = FindInstalledJdkHome();
        if (jdkHome is null)
            return null;

        await SetUserAsync("JAVA_HOME", jdkHome, ct).ConfigureAwait(false);
        return jdkHome;
    }

    public async Task<string?> TryConfigureAndroidHomeAsync(CancellationToken ct = default)
    {
        var sdkHome = _platform.DefaultAndroidSdkPath;
        var platformTools = Path.Combine(sdkHome, "platform-tools");
        if (!Directory.Exists(platformTools))
            return null;

        await SetUserAsync("ANDROID_HOME", sdkHome, ct).ConfigureAwait(false);
        await AppendToPathAsync(platformTools, ct).ConfigureAwait(false);
        return sdkHome;
    }

    // Recursively searches the platform's default JDK search path for a `bin/java(.exe)`
    // executable and returns its containing directory (the true JAVA_HOME) — this works across
    // JDK vendors (Temurin, Zulu, ...) without hardcoding any vendor-specific folder naming.
    private string? FindInstalledJdkHome()
    {
        var searchRoot = _platform.DefaultJdkSearchPath;
        if (!Directory.Exists(searchRoot))
            return null;

        var javaExeName = _platform.IsWindows ? "java.exe" : "java";

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(searchRoot, "*", SearchOption.AllDirectories))
            {
                if (File.Exists(Path.Combine(dir, "bin", javaExeName)))
                    return dir;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return null;
    }

    private string GetShellRcFile()
    {
        var shell = Environment.GetEnvironmentVariable("SHELL") ?? string.Empty;
        return shell.Contains("zsh", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(_platform.HomeDirectory, ".zshrc")
            : Path.Combine(_platform.HomeDirectory, ".bashrc");
    }

    private static async Task<bool> FileContainsAsync(string filePath, string text, CancellationToken ct)
    {
        if (!File.Exists(filePath))
            return false;

        var content = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        return content.Contains(text, StringComparison.Ordinal);
    }
}
