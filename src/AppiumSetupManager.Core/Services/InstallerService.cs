using System.Runtime.CompilerServices;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface IInstallerService
{
    IAsyncEnumerable<InstallStep> InstallAllAsync(CancellationToken ct = default);
    IAsyncEnumerable<InstallStep> InstallSelectedAsync(IEnumerable<string> componentNames, CancellationToken ct = default);

    /// <summary>
    /// Re-runs the install command for each named component regardless of whether DetectionService
    /// currently reports it as Found — unlike <see cref="InstallSelectedAsync"/>, which is correct to
    /// skip Found components for the Install screen's purposes. This is what lets the Updates screen
    /// actually re-run an already-installed-but-outdated-on-npm component (e.g. Appium) instead of the
    /// step silently coming back Skipped. Only NotApplicable entries are skipped here.
    /// </summary>
    IAsyncEnumerable<InstallStep> UpdateSelectedAsync(IEnumerable<string> componentNames, CancellationToken ct = default);
}

public sealed class InstallerService : IInstallerService
{
    private readonly ICommandRunner _runner;
    private readonly IPlatformAdapter _platform;
    private readonly IEnvironmentVariableManager _envManager;
    private readonly IDetectionService _detection;
    private readonly IHistoryService _history;

    public InstallerService(
        ICommandRunner runner,
        IPlatformAdapter platform,
        IEnvironmentVariableManager envManager,
        IDetectionService detection,
        IHistoryService history)
    {
        _runner = runner;
        _platform = platform;
        _envManager = envManager;
        _detection = detection;
        _history = history;
    }

    // ── Public methods ───────────────────────────────────────────────────────

    public async IAsyncEnumerable<InstallStep> InstallAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var scan = await _detection.ScanAllAsync(ct).ConfigureAwait(false);
        var skipSet = GetSkipSet(scan);
        await foreach (var step in ExecuteStepsAsync(InstallCatalog.All, skipSet, HistoryEntryType.Install, ct).WithCancellation(ct))
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
        await foreach (var step in ExecuteStepsAsync(filtered, skipSet, HistoryEntryType.Install, ct).WithCancellation(ct))
            yield return step;
    }

    public async IAsyncEnumerable<InstallStep> UpdateSelectedAsync(
        IEnumerable<string> componentNames,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var scan = await _detection.ScanAllAsync(ct).ConfigureAwait(false);
        var skipSet = GetUpdateSkipSet(scan);
        var nameSet = new HashSet<string>(componentNames, StringComparer.Ordinal);
        var filtered = InstallCatalog.All.Where(e => nameSet.Contains(e.ComponentName));
        await foreach (var step in ExecuteStepsAsync(filtered, skipSet, HistoryEntryType.Update, ct).WithCancellation(ct))
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

    // Skip set for InstallSelectedAsync/InstallAllAsync: a component already Found (or simply
    // NotApplicable on this platform) needs no install step.
    private static HashSet<string> GetSkipSet(IReadOnlyList<ComponentStatus> scan) =>
        BuildSkipSet(scan, skipFound: true);

    // Skip set for UpdateSelectedAsync: NEVER skip a Found component here — a Found component may
    // still be outdated per a live npm registry check the Updates screen already performed, and the
    // whole point of "Update" is to re-run the install command in that case. Only genuinely
    // NotApplicable entries (e.g. iOS-only components on Windows/Linux) are skipped.
    private static HashSet<string> GetUpdateSkipSet(IReadOnlyList<ComponentStatus> scan) =>
        BuildSkipSet(scan, skipFound: false);

    // Some catalog entries use a user-facing name that differs from the DetectionService probe
    // that actually verifies they're present — "JDK 21" is satisfied once the "JDK" probe reports
    // Found; "Android SDK" is satisfied once "ADB" does. Without this alias, a skip set can never
    // match these two entries against a scan, so they'd be re-installed on every run even when
    // already present (e.g. "Update" on an already-current ADB re-running the whole SDK cask install
    // for no reason every single time). The alias itself now lives on InstallCatalog so the Updates
    // screen's own catalog-name↔detection-name resolution shares the exact same source of truth.
    private static HashSet<string> BuildSkipSet(IReadOnlyList<ComponentStatus> scan, bool skipFound)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var status in scan)
        {
            if (status.State == DetectionState.NotApplicable || (skipFound && status.State == DetectionState.Found))
                set.Add(status.Name);
        }

        foreach (var (catalogName, detectionName) in InstallCatalog.DetectionNameAlias)
        {
            if (set.Contains(detectionName))
                set.Add(catalogName);
        }

        return set;
    }

    private async IAsyncEnumerable<InstallStep> ExecuteStepsAsync(
        IEnumerable<CatalogEntry> entries,
        HashSet<string> skipSet,
        HistoryEntryType entryType,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // "Installed Appium" / "Updated Appium" / "Failed to install Appium" / "Failed to update Appium".
        var verb = entryType == HistoryEntryType.Update ? "update" : "install";
        var verbPast = entryType == HistoryEntryType.Update ? "Updated" : "Installed";

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
                try { await _history.RecordAsync(entryType, $"Failed to {verb} {entry.ComponentName}", "Installation timed out after 10 minutes", null, ct).ConfigureAwait(false); }
                catch { /* swallow — non-fatal history-write failure */ }

                yield return new InstallStep(entry.ComponentName, command, InstallStepState.Failed,
                    "Installation timed out after 10 minutes", entry.SuggestedFix);
                continue;
            }

            if (!result.Success)
            {
                try { await _history.RecordAsync(entryType, $"Failed to {verb} {entry.ComponentName}", result.StdErr, null, ct).ConfigureAwait(false); }
                catch { /* swallow — non-fatal history-write failure */ }

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

            try { await _history.RecordAsync(entryType, $"{verbPast} {entry.ComponentName}", null, null, ct).ConfigureAwait(false); }
            catch { /* swallow — non-fatal history-write failure */ }

            yield return new InstallStep(entry.ComponentName, command, InstallStepState.Done, null, null);
        }
    }
}
