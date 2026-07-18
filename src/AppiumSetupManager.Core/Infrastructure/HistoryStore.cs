using System.Text.Json;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Infrastructure;

public interface IHistoryService
{
    /// <summary>
    /// Appends a new entry describing an install/update/repair/cleanup/setup action to the
    /// persisted history log.
    /// </summary>
    Task RecordAsync(HistoryEntryType type, string title, string? summary = null, string? rollbackTargetId = null, CancellationToken ct = default);

    /// <summary>
    /// Returns all recorded history entries, newest first (by <see cref="HistoryEntry.CreatedAtUtc"/>).
    /// Never throws — a missing or corrupt history file is treated as an empty list.
    /// </summary>
    Task<IReadOnlyList<HistoryEntry>> ListAsync(CancellationToken ct = default);
}

/// <summary>
/// Persists a bounded log of install/update/repair/cleanup/setup actions to a JSON file under the
/// user's home directory, so the History screen can show what this app has done over time. Copies
/// <see cref="EnvironmentBackupStore"/>'s atomic-write pattern exactly, but with a larger cap since
/// this file is written far more often than env-backups.
/// </summary>
public sealed class HistoryStore(IPlatformAdapter platform) : IHistoryService
{
    private const int MaxEntries = 500;

    public async Task RecordAsync(HistoryEntryType type, string title, string? summary = null, string? rollbackTargetId = null, CancellationToken ct = default)
    {
        var existing = await ListAsync(ct).ConfigureAwait(false);

        var entry = new HistoryEntry(
            Guid.NewGuid().ToString(),
            type,
            DateTimeOffset.UtcNow,
            title,
            summary,
            rollbackTargetId);

        // Newest first; cap at the newest MaxEntries so the history file never grows unbounded.
        var updated = existing
            .Append(entry)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(MaxEntries)
            .ToList();

        await WriteAsync(updated, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HistoryEntry>> ListAsync(CancellationToken ct = default)
    {
        var filePath = GetHistoryFilePath();

        try
        {
            if (!File.Exists(filePath))
                return [];

            var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json);
            if (entries is null)
                return [];

            return entries.OrderByDescending(e => e.CreatedAtUtc).ToList();
        }
        catch (JsonException)
        {
            // Corrupt/garbage file contents — treat as if no history exists rather than crashing the
            // caller. A subsequent RecordAsync will overwrite the corrupt file with valid JSON.
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private async Task WriteAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        await AtomicFile.WriteAllTextAsync(GetHistoryFilePath(), json, ct).ConfigureAwait(false);
    }

    private string GetHistoryFilePath() =>
        Path.Combine(platform.HomeDirectory, ".appiumsetupmanager", "history.json");
}
