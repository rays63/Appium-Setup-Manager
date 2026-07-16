using System.Text.Json;
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

    // `adb version` prints two version lines:
    //   "Android Debug Bridge version 1.0.41"   — frozen protocol version, not meaningful here
    //   "Version 37.0.0-14910828"                — the actual platform-tools build version
    // Anchored to line-start with a capital "V" so it targets the second line specifically.
    [GeneratedRegex(@"^Version (\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex AdbVersionRegex();

    [GeneratedRegex(@"Xcode (\d+)\.", RegexOptions.Compiled)]
    private static partial Regex XcodeVersionRegex();

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled)]
    private static partial Regex AppiumVersionRegex();

    // Appium driver names are bare identifiers, e.g. "uiautomator2", "xcuitest", "flutter".
    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9._-]*$", RegexOptions.Compiled)]
    private static partial Regex DriverNameRegex();

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

        // Single shared driver list fetch — --json gives machine-readable names + versions.
        var driverResult = await _runner.RunAsync("appium", "driver list --installed --json", pct).ConfigureAwait(false);

        var coreResults = await Task.WhenAll(
            ProbeHomebrewAsync(pct),
            ProbeNodeAsync(pct),
            ProbeNpmAsync(pct),
            ProbeJdkAsync(pct),
            ProbeJavaHomeAsync(pct),
            ProbeAdbAsync(pct),
            ProbeAndroidHomeAsync(pct),
            ProbeXcodeAsync(pct),
            ProbeXcodeCliToolsAsync(pct),
            ProbeAppiumAsync(pct),
            ProbeAppiumInspectorAsync(pct)
        ).ConfigureAwait(false);

        // Each installed Appium driver is surfaced as its own component (with version).
        var drivers = ParseInstalledDrivers(driverResult);

        return [.. coreResults, .. drivers];
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
                state == DetectionState.Outdated ? "34" : null, _platform.LocateExecutable("adb"), null);
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

    private async Task<ComponentStatus> ProbeHomebrewAsync(CancellationToken ct)
    {
        const string name = "Homebrew";
        if (!_platform.IsMacOs)
            return new ComponentStatus(name, DetectionState.NotApplicable, null, null, null, null);

        try
        {
            var result = await _runner.RunAsync("brew", "--version", ct).ConfigureAwait(false);
            if (result.TimedOut || !result.Success)
                return NotFound(name);

            var versionLine = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return new ComponentStatus(name, DetectionState.Found, versionLine, null, _platform.LocateExecutable("brew"), null);
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
                state == DetectionState.Outdated ? "2" : null, _platform.LocateExecutable("appium"), null);
        }
        catch (OperationCanceledException)
        {
            return NotFound(name);
        }
    }

    // The core drivers we always report on, mapped to their InstallCatalog component names so the
    // dashboard can offer a one-click install when they are missing. Extra drivers the user has
    // installed (e.g. flutter, mac2) are still surfaced dynamically alongside these.
    private static readonly (string CatalogName, string Key, bool MacOnly)[] KnownDrivers =
    [
        ("UiAutomator2 Driver", "uiautomator2", false),
        ("XCUITest Driver",     "xcuitest",     true),
    ];

    /// <summary>
    /// Turns the output of <c>appium driver list --installed --json</c> into one
    /// <see cref="ComponentStatus"/> per driver. The known core drivers are always reported —
    /// Found (with version), NotFound (so the dashboard can offer an install), or NotApplicable on
    /// the wrong platform — and any additional installed drivers are appended dynamically. Prefers
    /// the JSON payload; falls back to line parsing of human-readable output.
    /// </summary>
    private IReadOnlyList<ComponentStatus> ParseInstalledDrivers(CommandResult result)
    {
        var installed = new Dictionary<string, (string? Version, string? Path)>(StringComparer.OrdinalIgnoreCase);

        // A non-zero exit means Appium is missing or errored — its stderr must not be mistaken for a
        // driver list, so we leave `installed` empty and report the known drivers as NotFound.
        if (result.Success && !result.TimedOut)
        {
            var combined = string.Join('\n',
                new[] { result.StdOut, result.StdErr }.Where(s => !string.IsNullOrWhiteSpace(s)));

            foreach (var (name, version, path) in ParseDriverEntries(combined))
                installed[name] = (version, path);
        }

        var results = new List<ComponentStatus>();
        var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (catalogName, key, macOnly) in KnownDrivers)
        {
            knownKeys.Add(key);

            if (macOnly && !_platform.IsMacOs)
            {
                results.Add(new ComponentStatus(catalogName, DetectionState.NotApplicable, null, null, null, null));
                continue;
            }

            results.Add(installed.TryGetValue(key, out var info)
                ? new ComponentStatus(catalogName, DetectionState.Found, info.Version, null, info.Path, null)
                : new ComponentStatus(catalogName, DetectionState.NotFound, null, null, null, null));
        }

        // Any additional installed drivers that are not part of the known core set.
        foreach (var (name, info) in installed)
        {
            if (knownKeys.Contains(name))
                continue;
            results.Add(new ComponentStatus($"{name} driver", DetectionState.Found, info.Version, null, info.Path, null));
        }

        return results;
    }

    private static IReadOnlyList<(string Name, string? Version, string? Path)> ParseDriverEntries(string output) =>
        string.IsNullOrWhiteSpace(output)
            ? []
            : TryParseJsonDrivers(output) ?? ParseTextDrivers(output);

    private static List<(string Name, string? Version, string? Path)>? TryParseJsonDrivers(string output)
    {
        // Isolate the JSON object in case Appium prepends log noise to the line.
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(output[start..(end + 1)]);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var list = new List<(string, string?, string?)>();
            foreach (var driver in doc.RootElement.EnumerateObject())
            {
                string? version = null;
                string? installPath = null;
                if (driver.Value.ValueKind == JsonValueKind.Object)
                {
                    if (driver.Value.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String)
                        version = v.GetString();
                    if (driver.Value.TryGetProperty("installPath", out var p) && p.ValueKind == JsonValueKind.String)
                        installPath = p.GetString();
                }

                list.Add((driver.Name, version, installPath));
            }

            return list;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<(string Name, string? Version, string? Path)> ParseTextDrivers(string output)
    {
        var list = new List<(string, string?, string?)>();
        foreach (var raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Strip list bullets / status glyphs Appium prefixes each entry with.
            var line = raw.Trim().TrimStart('-', '*', '•', '✔', '✓', ' ', '\t').Trim();
            if (line.Length == 0 || line.Contains("Listing", StringComparison.OrdinalIgnoreCase))
                continue;

            // First token looks like "uiautomator2@3.10.0" or just "uiautomator2".
            var token = line.Split([' ', '\t', '['], StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
                ? parts[0]
                : string.Empty;
            if (token.Length == 0)
                continue;

            var at = token.IndexOf('@');
            var name = at > 0 ? token[..at] : token;
            var version = at > 0 ? token[(at + 1)..] : null;

            // Guard against decorative lines slipping through: driver names are bare identifiers.
            if (!DriverNameRegex().IsMatch(name))
                continue;

            list.Add((name, version, null));
        }

        return list;
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
