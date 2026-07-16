using System.Text.RegularExpressions;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface IDoctorService
{
    Task<IReadOnlyList<DoctorCheck>> RunChecksAsync(CancellationToken ct = default);
    Task<DoctorCheck> FixAndRecheckAsync(string checkName, CancellationToken ct = default);
}

public sealed partial class DoctorService : IDoctorService
{
    private readonly ICommandRunner _runner;
    private readonly IPlatformAdapter _platform;

    // ── Compiled regex fields ────────────────────────────────────────────────

    [GeneratedRegex(@"v(\d+)", RegexOptions.Compiled)]
    private static partial Regex NodeVersionRegex();

    [GeneratedRegex(@"version ""(\d+)[._]", RegexOptions.Compiled)]
    private static partial Regex JdkVersionRegex();

    [GeneratedRegex(@"^(\d+)", RegexOptions.Compiled)]
    private static partial Regex AppiumVersionRegex();

    // ── Constructor ──────────────────────────────────────────────────────────

    public DoctorService(ICommandRunner runner, IPlatformAdapter platform)
    {
        _runner = runner;
        _platform = platform;
    }

    // ── Public entry point ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<DoctorCheck>> RunChecksAsync(CancellationToken ct = default)
    {
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(TimeSpan.FromSeconds(5));
        var pct = probeCts.Token;

        // Single shared driver list fetch
        var driverResult = await _runner.RunAsync("appium", "driver list --installed", pct).ConfigureAwait(false);
        var driverOutput = driverResult.Success ? driverResult.StdOut : string.Empty;

        var results = await Task.WhenAll(
            CheckNodeAsync(pct),
            CheckNpmAsync(pct),
            CheckJdkAsync(pct),
            CheckJavaHomeAsync(pct),
            CheckAndroidHomeAsync(pct),
            CheckAdbAsync(pct),
            CheckAppiumAsync(pct),
            CheckUiAutomator2Async(driverOutput, pct),
            CheckXcuiTestAsync(driverOutput, pct),
            CheckAppiumInspectorAsync(pct)
        ).ConfigureAwait(false);

        // Filter out null (macOS-only checks on non-Mac) and return in stable order
        return results.Where(c => c is not null).ToList()!;
    }

    public async Task<DoctorCheck> FixAndRecheckAsync(string checkName, CancellationToken ct = default)
    {
        // Map check name to remediation command and recheck probe
        var (command, recheck) = checkName switch
        {
            "Appium"              => ("npm install -g appium",              (Func<CancellationToken, Task<DoctorCheck?>>)(CheckAppiumAsync)),
            "UiAutomator2 Driver" => ("appium driver install uiautomator2", CheckUiAutomator2ForFixAsync),
            "XCUITest Driver"     => ("appium driver install xcuitest",     CheckXcuiTestForFixAsync),
            _                     => throw new ArgumentException($"No auto-fix defined for check: {checkName}"),
        };

        // Run the fix command
        var parts = command.IndexOf(' ') is var i && i >= 0
            ? (command[..i], command[(i + 1)..])
            : (command, "");
        await _runner.RunAsync(parts.Item1, parts.Item2, ct).ConfigureAwait(false);

        // Re-run the check probe
        var updated = await recheck(ct).ConfigureAwait(false);
        return updated ?? Fail(checkName, "Unknown", "Re-check failed", null, false);
    }

    // ── Private per-check methods ────────────────────────────────────────────

    private async Task<DoctorCheck?> CheckNodeAsync(CancellationToken ct)
    {
        const string name = "Node.js";
        try
        {
            var result = await _runner.RunAsync("node", "--version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return Fail(name, "Dependencies", "Node.js not found", null, false);

            var match = NodeVersionRegex().Match(result.StdOut);
            if (!match.Success)
                return Fail(name, "Dependencies", "Node.js not found", null, false);

            var version = result.StdOut.Trim();
            if (!int.TryParse(match.Groups[1].Value, out var major))
                return Fail(name, "Dependencies", "Node.js not found", null, false);

            if (major >= 18)
                return new DoctorCheck(name, "Dependencies", CheckResult.Pass, $"Node.js {version} — OK", null, false);

            return new DoctorCheck(name, "Dependencies", CheckResult.Warn,
                $"Node.js {version} found but v18+ required", null, false);
        }
        catch (OperationCanceledException)
        {
            return Fail(name, "Dependencies", "Node.js not found", null, false);
        }
    }

    private async Task<DoctorCheck?> CheckNpmAsync(CancellationToken ct)
    {
        const string name = "npm";
        try
        {
            var result = await _runner.RunAsync("npm", "--version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return Fail(name, "Dependencies", "npm not found", null, false);

            var version = result.StdOut.Trim();
            return new DoctorCheck(name, "Dependencies", CheckResult.Pass, $"npm {version} — OK", null, false);
        }
        catch (OperationCanceledException)
        {
            return Fail(name, "Dependencies", "npm not found", null, false);
        }
    }

    private async Task<DoctorCheck?> CheckJdkAsync(CancellationToken ct)
    {
        const string name = "JDK";
        try
        {
            var result = await _runner.RunAsync("java", "-version", ct).ConfigureAwait(false);
            if (result.TimedOut)
                return Fail(name, "Dependencies", "JDK not found", null, false);

            var match = JdkVersionRegex().Match(result.StdErr);
            if (!match.Success)
                return Fail(name, "Dependencies", "JDK not found", null, false);

            var version = match.Groups[1].Value;
            if (!int.TryParse(version, out var major))
                return Fail(name, "Dependencies", "JDK not found", null, false);

            if (major >= 11)
                return new DoctorCheck(name, "Dependencies", CheckResult.Pass, $"JDK {version} — OK", null, false);

            return new DoctorCheck(name, "Dependencies", CheckResult.Warn,
                $"JDK {version} found but v11+ required", null, false);
        }
        catch (OperationCanceledException)
        {
            return Fail(name, "Dependencies", "JDK not found", null, false);
        }
    }

    private async Task<DoctorCheck?> CheckJavaHomeAsync(CancellationToken ct)
    {
        const string name = "JAVA_HOME";
        // No process spawn — environment variable check only.
        await Task.CompletedTask.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        try
        {
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrWhiteSpace(javaHome) || !Directory.Exists(javaHome))
                return Fail(name, "Environment", "JAVA_HOME not set or invalid", null, false);

            var javaExe = _platform.IsWindows
                ? Path.Combine(javaHome, "bin", "java.exe")
                : Path.Combine(javaHome, "bin", "java");

            if (!File.Exists(javaExe))
                return Fail(name, "Environment", "JAVA_HOME not set or invalid", null, false);

            return new DoctorCheck(name, "Environment", CheckResult.Pass, $"JAVA_HOME → {javaHome}", null, false);
        }
        catch (OperationCanceledException)
        {
            return Fail(name, "Environment", "JAVA_HOME not set or invalid", null, false);
        }
    }

    private async Task<DoctorCheck?> CheckAndroidHomeAsync(CancellationToken ct)
    {
        const string name = "ANDROID_HOME";
        // No process spawn — environment variable check only.
        await Task.CompletedTask.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        try
        {
            var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
            if (string.IsNullOrWhiteSpace(androidHome) || !Directory.Exists(androidHome))
                return Fail(name, "Environment", "ANDROID_HOME not set or points to missing path", null, false);

            var platformTools = Path.Combine(androidHome, "platform-tools");
            if (!Directory.Exists(platformTools))
                return Fail(name, "Environment", "ANDROID_HOME not set or points to missing path", null, false);

            return new DoctorCheck(name, "Environment", CheckResult.Pass, $"ANDROID_HOME → {androidHome}", null, false);
        }
        catch (OperationCanceledException)
        {
            return Fail(name, "Environment", "ANDROID_HOME not set or points to missing path", null, false);
        }
    }

    private async Task<DoctorCheck?> CheckAdbAsync(CancellationToken ct)
    {
        const string name = "ADB";
        try
        {
            var result = await _runner.RunAsync("adb", "version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return Fail(name, "Dependencies", "ADB not found — ensure ANDROID_HOME/platform-tools is on PATH", null, false);

            // Try to parse version from stdout
            var firstLine = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            var versionWord = firstLine.Contains("version", StringComparison.OrdinalIgnoreCase)
                ? firstLine.Split(' ').LastOrDefault() ?? "found"
                : "found";

            return new DoctorCheck(name, "Dependencies", CheckResult.Pass, $"ADB {versionWord} — OK", null, false);
        }
        catch (OperationCanceledException)
        {
            return Fail(name, "Dependencies", "ADB not found — ensure ANDROID_HOME/platform-tools is on PATH", null, false);
        }
    }

    private async Task<DoctorCheck?> CheckAppiumAsync(CancellationToken ct)
    {
        const string name = "Appium";
        try
        {
            var result = await _runner.RunAsync("appium", "--version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return Fail(name, "Dependencies", "Appium not found", "npm install -g appium", true);

            var versionStr = result.StdOut.Trim();
            var match = AppiumVersionRegex().Match(versionStr);
            if (!match.Success)
                return Fail(name, "Dependencies", "Appium not found", "npm install -g appium", true);

            if (!int.TryParse(match.Groups[1].Value, out var major))
                return Fail(name, "Dependencies", "Appium not found", "npm install -g appium", true);

            if (major >= 2)
                return new DoctorCheck(name, "Dependencies", CheckResult.Pass, $"Appium {versionStr} — OK", "npm install -g appium", true);

            return new DoctorCheck(name, "Dependencies", CheckResult.Warn,
                $"Appium {versionStr} found but v2+ required", "npm install -g appium", true);
        }
        catch (OperationCanceledException)
        {
            return Fail(name, "Dependencies", "Appium not found", "npm install -g appium", true);
        }
    }

    private async Task<DoctorCheck?> CheckUiAutomator2Async(string driverOutput, CancellationToken ct)
    {
        const string name = "UiAutomator2 Driver";
        await Task.CompletedTask.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var found = driverOutput.Contains("uiautomator2", StringComparison.OrdinalIgnoreCase);
        if (found)
            return new DoctorCheck(name, "Drivers", CheckResult.Pass, "UiAutomator2 driver installed",
                "appium driver install uiautomator2", true);

        return Fail(name, "Drivers", "UiAutomator2 driver not installed", "appium driver install uiautomator2", true);
    }

    private async Task<DoctorCheck?> CheckXcuiTestAsync(string driverOutput, CancellationToken ct)
    {
        const string name = "XCUITest Driver";
        await Task.CompletedTask.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        if (!_platform.IsMacOs)
            return null;

        var found = driverOutput.Contains("xcuitest", StringComparison.OrdinalIgnoreCase);
        if (found)
            return new DoctorCheck(name, "Drivers", CheckResult.Pass, "XCUITest driver installed",
                "appium driver install xcuitest", true);

        return Fail(name, "Drivers", "XCUITest driver not installed", "appium driver install xcuitest", true);
    }

    private async Task<DoctorCheck?> CheckAppiumInspectorAsync(CancellationToken ct)
    {
        const string name = "Appium Inspector";
        await Task.CompletedTask.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        try
        {
            const string systemApp = "/Applications/Appium Inspector.app";
            var userApp = Path.Combine(_platform.HomeDirectory, "Applications", "Appium Inspector.app");

            string? foundPath = null;
            if (Directory.Exists(systemApp))
                foundPath = systemApp;
            else if (Directory.Exists(userApp))
                foundPath = userApp;

            if (foundPath is not null)
                return new DoctorCheck(name, "Tools", CheckResult.Pass, $"Appium Inspector found at {foundPath}", null, false);

            return Fail(name, "Tools", "Appium Inspector not found", null, false);
        }
        catch (OperationCanceledException)
        {
            return Fail(name, "Tools", "Appium Inspector not found", null, false);
        }
    }

    // ── Fix recheck helpers ──────────────────────────────────────────────────

    private async Task<DoctorCheck?> CheckUiAutomator2ForFixAsync(CancellationToken ct)
    {
        var driverResult = await _runner.RunAsync("appium", "driver list --installed", ct).ConfigureAwait(false);
        var driverOutput = driverResult.Success ? driverResult.StdOut : string.Empty;
        return await CheckUiAutomator2Async(driverOutput, ct).ConfigureAwait(false);
    }

    private async Task<DoctorCheck?> CheckXcuiTestForFixAsync(CancellationToken ct)
    {
        var driverResult = await _runner.RunAsync("appium", "driver list --installed", ct).ConfigureAwait(false);
        var driverOutput = driverResult.Success ? driverResult.StdOut : string.Empty;
        return await CheckXcuiTestAsync(driverOutput, ct).ConfigureAwait(false);
    }

    // ── Static helpers ───────────────────────────────────────────────────────

    private static DoctorCheck Fail(string name, string group, string description, string? cmd, bool canFix) =>
        new(name, group, CheckResult.Fail, description, cmd, canFix);
}
