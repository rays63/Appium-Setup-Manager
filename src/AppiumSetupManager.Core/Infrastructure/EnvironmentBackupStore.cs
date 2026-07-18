using System.Text.Json;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Infrastructure;

public interface IEnvironmentBackupStore
{
    /// <summary>
    /// Records the value an environment variable held immediately before this app changed it.
    /// </summary>
    Task RecordAsync(string variableName, string previousValue, string reason, CancellationToken ct = default);

    /// <summary>
    /// Returns all recorded backup entries, newest first (by <see cref="EnvironmentBackupEntry.CreatedAtUtc"/>).
    /// Never throws — a missing or corrupt backup file is treated as an empty list.
    /// </summary>
    Task<IReadOnlyList<EnvironmentBackupEntry>> ListAsync(CancellationToken ct = default);

    /// <summary>Looks up a single backup entry by <see cref="EnvironmentBackupEntry.Id"/>.</summary>
    Task<EnvironmentBackupEntry?> FindAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// Persists a bounded history of environment variable overwrites to a JSON file under the user's
/// home directory, so the Environment screen can show (and eventually restore) what a previous
/// value was before this app changed it.
/// </summary>
public sealed class EnvironmentBackupStore : IEnvironmentBackupStore
{
    private const int MaxEntries = 50;

    private readonly IPlatformAdapter _platform;

    public EnvironmentBackupStore(IPlatformAdapter platform)
    {
        _platform = platform;
    }

    public async Task RecordAsync(string variableName, string previousValue, string reason, CancellationToken ct = default)
    {
        var existing = await ListAsync(ct).ConfigureAwait(false);

        var entry = new EnvironmentBackupEntry(
            Guid.NewGuid().ToString(),
            variableName,
            previousValue,
            DateTimeOffset.UtcNow,
            reason);

        // Newest first; cap at the newest MaxEntries so the backup file never grows unbounded.
        var updated = existing
            .Append(entry)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(MaxEntries)
            .ToList();

        await WriteAsync(updated, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EnvironmentBackupEntry>> ListAsync(CancellationToken ct = default)
    {
        var filePath = GetBackupFilePath();

        try
        {
            if (!File.Exists(filePath))
                return [];

            var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            var entries = JsonSerializer.Deserialize<List<EnvironmentBackupEntry>>(json);
            if (entries is null)
                return [];

            return entries.OrderByDescending(e => e.CreatedAtUtc).ToList();
        }
        catch (JsonException)
        {
            // Corrupt/garbage file contents — treat as if no backups exist rather than crashing the
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

    public async Task<EnvironmentBackupEntry?> FindAsync(string id, CancellationToken ct = default)
    {
        var entries = await ListAsync(ct).ConfigureAwait(false);
        return entries.FirstOrDefault(e => e.Id == id);
    }

    private async Task WriteAsync(IReadOnlyList<EnvironmentBackupEntry> entries, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        await AtomicFile.WriteAllTextAsync(GetBackupFilePath(), json, ct).ConfigureAwait(false);
    }

    private string GetBackupFilePath() =>
        Path.Combine(_platform.HomeDirectory, ".appiumsetupmanager", "env-backups.json");
}
