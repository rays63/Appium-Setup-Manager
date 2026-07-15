using System.Text.RegularExpressions;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface IDetectionService
{
    Task<IReadOnlyList<ComponentStatus>> ScanAllAsync(CancellationToken ct = default);
}

public sealed partial class DetectionService : IDetectionService
{
    private readonly ICommandRunner _runner;
    private readonly IPlatformAdapter _platform;

    // ── Compiled regex fields ────────────────────────────────────────────────

    [GeneratedRegex(@"v(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled)]
    private static partial Regex NodeVersionRegex();

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled)]
    private static partial Regex NpmVersionRegex();

    // java -version writes to stderr: openjdk version "21.0.3" or java version "11.0.18"
    [GeneratedRegex(@"version ""(\d+)[._]", RegexOptions.Compiled)]
    private static partial Regex JdkVersionRegex();

    [GeneratedRegex(@"version (\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled)]
    private static partial Regex AdbVersionRegex();

    [GeneratedRegex(@"Xcode (\d+)\.", RegexOptions.Compiled)]
    private static partial Regex XcodeVersionRegex();

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled)]
    private static partial Regex AppiumVersionRegex();

    // ── Constructor ─────────────────────────────────────────────────────────

    public DetectionService(ICommandRunner runner, IPlatformAdapter platform)
    {
        _runner = runner;
        _platform = platform;
    }

    // ── Public entry point ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<ComponentStatus>> ScanAllAsync(CancellationToken ct = default)
    {
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(TimeSpan.FromSeconds(5));
        var pct = probeCts.Token;

        // Single shared driver list fetch — avoids spawning appium twice.
        var driverResult = await _runner.RunAsync("appium", "driver list --installed", pct).ConfigureAwait(false);
        var driverOutput = driverResult.Success ? driverResult.StdOut : string.Empty;

        var results = await Task.WhenAll(
            ProbeNodeAsync(pct),
            ProbeNpmAsync(pct),
            ProbeJdkAsync(pct),
            ProbeJavaHomeAsync(pct),
            ProbeAdbAsync(pct),
            ProbeAndroidHomeAsync(pct),
            ProbeXcodeAsync(pct),
            ProbeXcodeCliToolsAsync(pct),
            ProbeAppiumAsync(pct),
            Task.FromResult(ProbeUiAutomator2(driverOutput)),
            Task.FromResult(ProbeXcuiTest(driverOutput)),
            ProbeAppiumInspectorAsync(pct)
        ).ConfigureAwait(false);

        return results;
    }

    // ── Private probes ───────────────────────────────────────────────────────

    private async Task<ComponentStatus> ProbeNodeAsync(CancellationToken ct)
    {
        const string name = "Node.js";
        try
        {
            var result = await _runner.RunAsync("node", "--version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return NotFound(name);

            var match = NodeVersionRegex().Match(result.StdOut);
            if (!match.Success)
                return NotFound(name);

            var state = ClassifyVersion(result.StdOut.Trim(), 18);
            return new ComponentStatus(name, state, result.StdOut.Trim(),
                state == DetectionState.Outdated ? "18" : null, null, null);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    private async Task<ComponentStatus> ProbeNpmAsync(CancellationToken ct)
    {
        const string name = "npm";
        try
        {
            var result = await _runner.RunAsync("npm", "--version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return NotFound(name);

            var state = ClassifyVersion(result.StdOut.Trim(), 9);
            return new ComponentStatus(name, state, result.StdOut.Trim(),
                state == DetectionState.Outdated ? "9" : null, null, null);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    private async Task<ComponentStatus> ProbeJdkAsync(CancellationToken ct)
    {
        const string name = "JDK";
        try
        {
            // java -version writes to stderr, not stdout — this is standard JVM behaviour.
            var result = await _runner.RunAsync("java", "-version", ct).ConfigureAwait(false);
            if (result.TimedOut)
                return NotFound(name);

            var match = JdkVersionRegex().Match(result.StdErr);
            if (!match.Success)
                return NotFound(name);

            var majorStr = match.Groups[1].Value;
            var state = ClassifyVersion(majorStr, 11);
            // Report the full stderr first-line as the installed version string.
            var versionLine = result.StdErr.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            return new ComponentStatus(name, state, versionLine,
                state == DetectionState.Outdated ? "11" : null, null, null);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    private async Task<ComponentStatus> ProbeJavaHomeAsync(CancellationToken ct)
    {
        const string name = "JAVA_HOME";
        // No process spawn — environment variable check only.
        await Task.CompletedTask.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        try
        {
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrWhiteSpace(javaHome) || !Directory.Exists(javaHome))
                return NotFound(name, envVar: "JAVA_HOME");

            var javaExe = _platform.IsWindows
                ? Path.Combine(javaHome, "bin", "java.exe")
                : Path.Combine(javaHome, "bin", "java");

            var state = File.Exists(javaExe) ? DetectionState.Found : DetectionState.NotFound;
            return new ComponentStatus(name, state, null, null, javaHome, "JAVA_HOME");
        }
        catch (OperationCanceledException)
        {
            return NotFound(name, envVar: "JAVA_HOME");
        }
    }

    private async Task<ComponentStatus> ProbeAdbAsync(CancellationToken ct)
    {
        const string name = "ADB";
        try
        {
            var result = await _runner.RunAsync("adb", "version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return NotFound(name);

            var match = AdbVersionRegex().Match(result.StdOut);
            if (!match.Success)
                return NotFound(name);

            var versionStr = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
            if (!int.TryParse(match.Groups[1].Value, out var major))
                return NotFound(name);

            var state = major >= 34 ? DetectionState.Found : DetectionState.Outdated;
            return new ComponentStatus(name, state, versionStr,
                state == DetectionState.Outdated ? "34" : null, null, null);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    private async Task<ComponentStatus> ProbeAndroidHomeAsync(CancellationToken ct)
    {
        const string name = "ANDROID_HOME";
        await Task.CompletedTask.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        try
        {
            var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
            if (string.IsNullOrWhiteSpace(androidHome) || !Directory.Exists(androidHome))
                return NotFound(name, envVar: "ANDROID_HOME");

            var platformTools = Path.Combine(androidHome, "platform-tools");
            var state = Directory.Exists(platformTools) ? DetectionState.Found : DetectionState.NotFound;
            return new ComponentStatus(name, state, null, null, androidHome, "ANDROID_HOME");
        }
        catch (OperationCanceledException)
        {
            return NotFound(name, envVar: "ANDROID_HOME");
        }
    }

    private async Task<ComponentStatus> ProbeXcodeAsync(CancellationToken ct)
    {
        const string name = "Xcode";
        if (!_platform.IsMacOs)
            return new ComponentStatus(name, DetectionState.NotApplicable, null, null, null, null);

        try
        {
            var result = await _runner.RunAsync("xcodebuild", "-version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return NotFound(name);

            var match = XcodeVersionRegex().Match(result.StdOut);
            if (!match.Success)
                return NotFound(name);

            var majorStr = match.Groups[1].Value;
            var state = ClassifyVersion(majorStr, 14);
            return new ComponentStatus(name, state, result.StdOut.Split('\n')[0].Trim(),
                state == DetectionState.Outdated ? "14" : null, null, null);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    private async Task<ComponentStatus> ProbeXcodeCliToolsAsync(CancellationToken ct)
    {
        const string name = "Xcode CLI Tools";
        if (!_platform.IsMacOs)
            return new ComponentStatus(name, DetectionState.NotApplicable, null, null, null, null);

        try
        {
            var result = await _runner.RunAsync("xcode-select", "-p", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success || string.IsNullOrWhiteSpace(result.StdOut))
                return NotFound(name);

            return new ComponentStatus(name, DetectionState.Found, null, null, result.StdOut.Trim(), null);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    private async Task<ComponentStatus> ProbeAppiumAsync(CancellationToken ct)
    {
        const string name = "Appium";
        try
        {
            var result = await _runner.RunAsync("appium", "--version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return NotFound(name);

            var match = AppiumVersionRegex().Match(result.StdOut.Trim());
            if (!match.Success)
                return NotFound(name);

            var state = ClassifyVersion(result.StdOut.Trim(), 2);
            return new ComponentStatus(name, state, result.StdOut.Trim(),
                state == DetectionState.Outdated ? "2" : null, null, null);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    private ComponentStatus ProbeUiAutomator2(string driverListOutput)
    {
        const string name = "UiAutomator2 Driver";
        var found = driverListOutput.Contains("uiautomator2", StringComparison.OrdinalIgnoreCase);
        return new ComponentStatus(name,
            found ? DetectionState.Found : DetectionState.NotFound,
            null, null, null, null);
    }

    private ComponentStatus ProbeXcuiTest(string driverListOutput)
    {
        const string name = "XCUITest Driver";
        if (!_platform.IsMacOs)
            return new ComponentStatus(name, DetectionState.NotApplicable, null, null, null, null);

        var found = driverListOutput.Contains("xcuitest", StringComparison.OrdinalIgnoreCase);
        return new ComponentStatus(name,
            found ? DetectionState.Found : DetectionState.NotFound,
            null, null, null, null);
    }

    private async Task<ComponentStatus> ProbeAppiumInspectorAsync(CancellationToken ct)
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

            return foundPath is not null
                ? new ComponentStatus(name, DetectionState.Found, null, null, foundPath, null)
                : NotFound(name);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the major version number from a version string (e.g. "v18.2.0", "21.0.3", "11")
    /// and classifies it as Found, Outdated, or NotFound relative to <paramref name="minMajor"/>.
    /// </summary>
    private static DetectionState ClassifyVersion(string? versionStr, int minMajor)
    {
        if (string.IsNullOrWhiteSpace(versionStr))
            return DetectionState.NotFound;

        // Strip a leading 'v' so "v18.2.0" → "18.2.0"
        var s = versionStr.TrimStart('v');

        // Take only the numeric segment before the first non-digit/non-dot character.
        var dotIndex = s.IndexOf('.');
        var majorPart = dotIndex >= 0 ? s[..dotIndex] : s;

        if (!int.TryParse(majorPart, out var major))
            return DetectionState.NotFound;

        return major >= minMajor ? DetectionState.Found : DetectionState.Outdated;
    }

    private static ComponentStatus NotFound(string name, string? envVar = null) =>
        new(name, DetectionState.NotFound, null, null, null, envVar);
}
