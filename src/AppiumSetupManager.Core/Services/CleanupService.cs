using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface ICleanupService
{
    Task<long> DeleteAsync(IEnumerable<StorageItem> items, CancellationToken ct = default);
}

/// <summary>
/// Deletes the storage items the user confirmed. Every freed-bytes number is MEASURED — summed
/// from files actually deleted (manual walk) or from a before/after size delta (CLI-backed) —
/// never assumed from the scan. Each item is fault-isolated: a failure skips that item and is
/// counted, never aborting the run. Paths are hard-validated against the home/SDK roots before
/// anything is touched, and only a whitelisted set of executables can ever be run.
/// </summary>
public sealed class CleanupService : ICleanupService
{
    /// <summary>
    /// Hard whitelist of delete tools. A malformed or tampered StorageItem naming any other
    /// executable is treated as if it had no tool at all (manual walk) — never run.
    /// </summary>
    private static readonly string[] AllowedDeleteTools = ["npm", "avdmanager"];

    private readonly ICommandRunner _runner;
    private readonly IHistoryService _history;
    private readonly IPlatformAdapter _platform;

    public CleanupService(ICommandRunner runner, IHistoryService history, IPlatformAdapter platform)
    {
        _runner = runner;
        _history = history;
        _platform = platform;
    }

    public async Task<long> DeleteAsync(IEnumerable<StorageItem> items, CancellationToken ct = default)
    {
        var list = items.ToList();
        if (list.Count == 0)
            return 0; // Nothing requested — no history entry either.

        long freedBytes = 0;
        var failedItems = 0;

        try
        {
            foreach (var item in list)
            {
                ct.ThrowIfCancellationRequested();

                var (itemFreed, itemFailed) = await DeleteItemAsync(item, ct).ConfigureAwait(false);
                freedBytes += itemFreed;
                if (itemFailed)
                    failedItems++;
            }
        }
        catch (OperationCanceledException)
        {
            // Record the partial result FIRST (with CancellationToken.None so a cancelled run
            // still gets its history entry), then let the OCE propagate — the ViewModel catches
            // it and re-scans.
            await RecordHistoryAsync(freedBytes, list.Count, failedItems, cancelled: true).ConfigureAwait(false);
            throw;
        }

        await RecordHistoryAsync(freedBytes, list.Count, failedItems, cancelled: false).ConfigureAwait(false);
        return freedBytes;
    }

    /// <summary>Deletes one item; returns measured freed bytes and whether it (partially) failed.</summary>
    private async Task<(long FreedBytes, bool Failed)> DeleteItemAsync(StorageItem item, CancellationToken ct)
    {
        try
        {
            if (!TryValidatePath(item.Path, out var fullPath))
                return (0, true); // Refused: outside the allowed roots — nothing is touched.

            if (item.DeleteToolExecutable is not null && IsWhitelistedTool(item.DeleteToolExecutable))
            {
                var (cliFreed, cliSucceeded) = await TryCliDeleteAsync(item, fullPath, ct).ConfigureAwait(false);
                if (cliSucceeded)
                    return (cliFreed, false);
                // CLI failed — fall through to the manual walk.
            }

            return DeleteManually(fullPath, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // Per-item fault isolation: count it as failed, keep going with the rest.
            return (0, true);
        }
    }

    /// <summary>
    /// Runs the whitelisted delete tool and measures freed bytes as before − after (directory
    /// gone entirely → the whole before size). Returns Succeeded=false on non-zero exit or
    /// exception so the caller falls back to the manual walk.
    /// </summary>
    private async Task<(long FreedBytes, bool Succeeded)> TryCliDeleteAsync(StorageItem item, string fullPath, CancellationToken ct)
    {
        var sizeBefore = DirectoryMetrics.GetSizeBytes(fullPath, ct);

        try
        {
            var result = await _runner.RunAsync(item.DeleteToolExecutable!, item.DeleteToolArguments ?? string.Empty, ct)
                .ConfigureAwait(false);
            if (!result.Success)
                return (0, false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return (0, false);
        }

        var sizeAfter = Directory.Exists(fullPath) ? DirectoryMetrics.GetSizeBytes(fullPath, ct) : 0;
        return (Math.Max(0, sizeBefore - sizeAfter), true);
    }

    /// <summary>Manual bottom-up walk; for an AVD directory the sibling &lt;name&gt;.ini is removed too.</summary>
    private (long FreedBytes, bool Failed) DeleteManually(string fullPath, CancellationToken ct)
    {
        var (freed, failedEntries) = DirectoryMetrics.DeleteRecursive(fullPath, ct);

        // AVD fallback contract: `Pixel_7.avd` leaves a dangling sibling `Pixel_7.ini` behind
        // unless we remove it (avdmanager would have handled both).
        if (fullPath.EndsWith(".avd", PathComparison))
        {
            var iniPath = Path.ChangeExtension(fullPath, ".ini");
            try
            {
                if (File.Exists(iniPath))
                {
                    var iniLength = new FileInfo(iniPath).Length;
                    File.Delete(iniPath);
                    freed += iniLength;
                }
            }
            catch (IOException) { failedEntries++; }
            catch (UnauthorizedAccessException) { failedEntries++; }
        }

        return (freed, failedEntries > 0);
    }

    /// <summary>
    /// Hard validation before anything is touched: the path must be fully qualified, live
    /// STRICTLY under the home directory or the Android SDK root (never equal to either), and
    /// sit at least two segments below that root — so `~/.gradle` alone is refused but
    /// `~/.gradle/caches` passes.
    /// </summary>
    private bool TryValidatePath(string rawPath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPath) || !Path.IsPathFullyQualified(rawPath))
            return false;

        string candidate;
        try
        {
            candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rawPath));
        }
        catch (Exception)
        {
            return false; // Unparseable path — refuse.
        }

        foreach (var rootRaw in new[] { _platform.HomeDirectory, _platform.DefaultAndroidSdkPath })
        {
            if (string.IsNullOrWhiteSpace(rootRaw))
                continue;

            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootRaw));

            if (candidate.Equals(root, PathComparison))
                return false; // Never the root itself.

            if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
                continue; // Trailing-separator guard: "/home/userX" must not match root "/home/user".

            var relative = candidate[(root.Length + 1)..];
            var segments = relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2)
            {
                fullPath = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>Case-sensitivity for path comparisons: only Linux filesystems are case-sensitive by default.</summary>
    private StringComparison PathComparison =>
        _platform.IsLinux ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>Whitelist check on the executable's base name, so full paths and .bat/.cmd wrappers match.</summary>
    private static bool IsWhitelistedTool(string executable)
    {
        var baseName = Path.GetFileNameWithoutExtension(executable);
        return AllowedDeleteTools.Contains(baseName, StringComparer.OrdinalIgnoreCase);
    }

    private Task RecordHistoryAsync(long freedBytes, int itemCount, int failedItems, bool cancelled)
    {
        var summary = cancelled
            ? $"Cancelled after freeing {DirectoryMetrics.Humanize(freedBytes)} ({itemCount} item(s) requested)"
            : $"Freed {DirectoryMetrics.Humanize(freedBytes)} across {itemCount} item(s)";

        if (failedItems > 0)
            summary += $"; {failedItems} item(s) failed or partially failed";

        // CancellationToken.None on purpose: a cancelled cleanup must still leave a history entry.
        return _history.RecordAsync(HistoryEntryType.Cleanup, "Cleaned up storage", summary, null, CancellationToken.None);
    }
}
