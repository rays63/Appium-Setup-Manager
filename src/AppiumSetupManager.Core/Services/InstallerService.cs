using System.Runtime.CompilerServices;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface IInstallerService
{
    IAsyncEnumerable<InstallStep> InstallAllAsync(CancellationToken ct = default);
    IAsyncEnumerable<InstallStep> InstallSelectedAsync(IEnumerable<string> componentNames, CancellationToken ct = default);
}

public sealed class InstallerService : IInstallerService
{
    private readonly ICommandRunner _runner;
    private readonly IPlatformAdapter _platform;
    private readonly IEnvironmentVariableManager _envManager;
    private readonly IDetectionService _detection;

    public InstallerService(
        ICommandRunner runner,
        IPlatformAdapter platform,
        IEnvironmentVariableManager envManager,
        IDetectionService detection)
    {
        _runner = runner;
        _platform = platform;
        _envManager = envManager;
        _detection = detection;
    }

    // ── Public methods ───────────────────────────────────────────────────────

    public async IAsyncEnumerable<InstallStep> InstallAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var scan = await _detection.ScanAllAsync(ct).ConfigureAwait(false);
        var skipSet = GetSkipSet(scan);
        await foreach (var step in ExecuteStepsAsync(InstallCatalog.All, skipSet, ct).WithCancellation(ct))
            yield return step;
    }

    public async IAsyncEnumerable<InstallStep> InstallSelectedAsync(
        IEnumerable<string> componentNames,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var scan = await _detection.ScanAllAsync(ct).ConfigureAwait(false);
        var skipSet = GetSkipSet(scan);
        var nameSet = new HashSet<string>(componentNames, StringComparer.Ordinal);
        var filtered = InstallCatalog.All.Where(e => nameSet.Contains(e.ComponentName));
        await foreach (var step in ExecuteStepsAsync(filtered, skipSet, ct).WithCancellation(ct))
            yield return step;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private string? ResolveCommand(CatalogEntry entry)
    {
        if (entry.MacOnly && !_platform.IsMacOs)
            return null;

        if (_platform.IsMacOs)
            return entry.MacCommand;
        if (_platform.IsWindows)
            return entry.WindowsCommand;
        return entry.LinuxCommand;
    }

    // Some catalog entries use a user-facing name that differs from the DetectionService probe
    // that actually verifies they're present — "JDK 21" is satisfied once the "JDK" probe reports
    // Found; "Android SDK" is satisfied once "ADB" does. Without this alias, GetSkipSet can never
    // match these two entries against a scan, so they'd be re-installed on every run even when
    // already present (e.g. "Update" on an already-current ADB re-running the whole SDK cask install
    // for no reason every single time).
    private static readonly Dictionary<string, string> SkipCheckAlias = new(StringComparer.Ordinal)
    {
        ["JDK 21"]      = "JDK",
        ["Android SDK"] = "ADB",
    };

    private static HashSet<string> GetSkipSet(IReadOnlyList<ComponentStatus> scan)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var status in scan)
        {
            if (status.State == DetectionState.Found || status.State == DetectionState.NotApplicable)
                set.Add(status.Name);
        }

        foreach (var (catalogName, detectionName) in SkipCheckAlias)
        {
            if (set.Contains(detectionName))
                set.Add(catalogName);
        }

        return set;
    }

    private async IAsyncEnumerable<InstallStep> ExecuteStepsAsync(
        IEnumerable<CatalogEntry> entries,
        HashSet<string> skipSet,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException(ct);

            var command = ResolveCommand(entry);

            // Null command = platform mismatch (MacOnly on non-Mac, or no command defined for platform).
            // Emit nothing — entry is silently omitted.
            if (command is null)
                continue;

            // Empty string sentinel = bundled component (e.g. npm bundled with Node.js).
            if (command == string.Empty)
            {
                yield return new InstallStep(entry.ComponentName, string.Empty, InstallStepState.Skipped, null, entry.SuggestedFix);
                continue;
            }

            // Component already installed or not applicable — skip.
            if (skipSet.Contains(entry.ComponentName))
            {
                yield return new InstallStep(entry.ComponentName, command, InstallStepState.Skipped, null, null);
                continue;
            }

            // Emit Pending before starting.
            yield return new InstallStep(entry.ComponentName, command, InstallStepState.Pending, null, null);

            // Create a per-step CTS with 10-minute ceiling, linked to the caller's token.
            using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            stepCts.CancelAfter(TimeSpan.FromMinutes(10));
            var stepCt = stepCts.Token;

            yield return new InstallStep(entry.ComponentName, command, InstallStepState.Running, null, null);

            // Split command into executable + arguments on the first space.
            var spaceIdx = command.IndexOf(' ');
            var exe = spaceIdx < 0 ? command : command[..spaceIdx];
            var args = spaceIdx < 0 ? string.Empty : command[(spaceIdx + 1)..];

            var result = await _runner.RunAsync(exe, args, stepCt).ConfigureAwait(false);

            if (result.TimedOut)
            {
                yield return new InstallStep(entry.ComponentName, command, InstallStepState.Failed,
                    "Installation timed out after 10 minutes", entry.SuggestedFix);
                continue;
            }

            if (!result.Success)
            {
                yield return new InstallStep(entry.ComponentName, command, InstallStepState.Failed,
                    result.StdErr, entry.SuggestedFix);
                continue;
            }

            // Post-install env var / PATH setup.
            if (entry.PostInstallEnvVar == "ANDROID_HOME")
            {
                try
                {
                    await _envManager.SetUserAsync("ANDROID_HOME", _platform.DefaultAndroidSdkPath, ct)
                        .ConfigureAwait(false);
                }
                catch { /* swallow — non-fatal env setup failure */ }
            }
            else if (entry.PostInstallEnvVar == "JAVA_HOME")
            {
                try
                {
                    // Best-effort: locate the JDK we (or the user) just installed and point JAVA_HOME at it.
                    await _envManager.TryConfigureJavaHomeAsync(ct).ConfigureAwait(false);
                }
                catch { /* swallow — non-fatal env setup failure */ }
            }

            if (entry.PostInstallPathDir == "<platform-tools>")
            {
                try
                {
                    await _envManager.AppendToPathAsync(
                        Path.Combine(_platform.DefaultAndroidSdkPath, "platform-tools"), ct)
                        .ConfigureAwait(false);
                }
                catch { /* swallow — non-fatal PATH update failure */ }
            }

            yield return new InstallStep(entry.ComponentName, command, InstallStepState.Done, null, null);
        }
    }
}
