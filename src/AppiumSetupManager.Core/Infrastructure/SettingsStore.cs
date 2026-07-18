using System.Text.Json;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Infrastructure;

/// <summary>
/// Persisted user preferences. The API is deliberately synchronous: ThemeService needs a sync
/// read at startup (before any UI exists), the file is tiny, and writes are rare UI-thread
/// toggles.
///
/// Deliberately no <c>Save(AppSettings)</c> overload: the Update-lambda API exists specifically
/// so two writers can never clobber each other with stale snapshots — every write is expressed
/// as a mutation of the store's current value, not a replacement with whatever the caller read
/// earlier.
/// </summary>
public interface ISettingsStore
{
    /// <summary>The current settings. Never null — missing/corrupt file falls back to defaults.</summary>
    AppSettings Current { get; }

    /// <summary>
    /// Applies <paramref name="mutate"/> to the cached current settings, then persists the result
    /// atomically. Callers pass e.g. <c>s => s with { Theme = AppThemeMode.Light }</c>.
    /// </summary>
    void Update(Func<AppSettings, AppSettings> mutate);
}

/// <summary>
/// File-backed settings store: single read of ~/.appiumsetupmanager/settings.json at construction
/// (missing/corrupt/unreadable → defaults, never throws), atomic write-through on every Update.
/// </summary>
public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public SettingsStore(IPlatformAdapter platform)
    {
        _filePath = Path.Combine(platform.HomeDirectory, ".appiumsetupmanager", "settings.json");
        Current = Read(_filePath);
    }

    public AppSettings Current { get; private set; }

    public void Update(Func<AppSettings, AppSettings> mutate)
    {
        Current = mutate(Current);
        AtomicFile.WriteAllText(_filePath, JsonSerializer.Serialize(Current, SerializerOptions));
    }

    private static AppSettings Read(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return new AppSettings();

            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Corrupt/garbage file contents (including an unknown enum string) — fall back to
            // defaults rather than crashing startup. The next Update overwrites it with valid JSON.
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }
}
