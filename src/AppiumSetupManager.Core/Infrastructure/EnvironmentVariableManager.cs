using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Infrastructure;

public interface IEnvironmentVariableManager
{
    string? Get(string name);
    Task SetUserAsync(string name, string value, CancellationToken ct = default);
    Task AppendToPathAsync(string directory, CancellationToken ct = default);
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
