using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface IStorageService
{
    Task<IReadOnlyList<StorageItem>> ScanAsync(CancellationToken ct = default);
}

/// <summary>
/// Discovers reclaimable disk usage across the mobile-automation toolchain (npm, Appium, Gradle,
/// Android SDK, and — on macOS — Xcode/Simulator/Homebrew caches). All roots come from
/// <see cref="IPlatformAdapter"/> so tests can substitute a temp home; every category is scanned
/// inside its own fault-isolation boundary so one unreadable directory never sinks the scan.
/// </summary>
public sealed class StorageService : IStorageService
{
    /// <summary>An AVD untouched for this long is flagged Review instead of InUse.</summary>
    private static readonly TimeSpan AvdStaleAge = TimeSpan.FromDays(30);

    private readonly IPlatformAdapter _platform;

    public StorageService(IPlatformAdapter platform) => _platform = platform;

    public Task<IReadOnlyList<StorageItem>> ScanAsync(CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<StorageItem>>(() => Scan(ct), ct);

    private List<StorageItem> Scan(CancellationToken ct)
    {
        var home = _platform.HomeDirectory;
        var items = new List<StorageItem>();

        // npm keeps its content-addressed cache under _cacache; %LocalAppData% on Windows.
        var npmCache = _platform.IsWindows
            ? Path.Combine(home, "AppData", "Local", "npm-cache", "_cacache")
            : Path.Combine(home, ".npm", "_cacache");
        AddDirectoryItem(items, "npm cache", "npm package cache", npmCache, RiskLevel.Low,
            deleteToolExecutable: _platform.LocateExecutable("npm") ?? "npm",
            deleteToolArguments: "cache clean --force", ct: ct);

        // Deliberately ~/.appium/logs only — NEVER the whole ~/.appium, which contains the
        // installed drivers this app exists to manage.
        AddDirectoryItem(items, "Appium logs", "Appium log files",
            Path.Combine(home, ".appium", "logs"), RiskLevel.Low, null, null, ct);

        AddDirectoryItem(items, "Gradle caches", "Gradle build caches",
            Path.Combine(home, ".gradle", "caches"), RiskLevel.Low, null, null, ct);

        ScanAvds(items, home, ct);
        ScanSystemImages(items, ct);

        if (_platform.IsMacOs)
        {
            AddDirectoryItem(items, "Xcode DerivedData", "Xcode build products and indexes",
                Path.Combine(home, "Library", "Developer", "Xcode", "DerivedData"), RiskLevel.Low, null, null, ct);

            AddDirectoryItem(items, "iOS Simulator caches", "CoreSimulator caches",
                Path.Combine(home, "Library", "Developer", "CoreSimulator", "Caches"), RiskLevel.Low, null, null, ct);

            // Manual walk on purpose, NOT `brew cleanup`: cleanup also prunes the Cellar, so the
            // freed-bytes number would not match the size we scanned here.
            AddDirectoryItem(items, "Homebrew cache", "Homebrew download cache",
                Path.Combine(home, "Library", "Caches", "Homebrew"), RiskLevel.Low, null, null, ct);
        }

        return items.OrderByDescending(i => i.SizeBytes).ToList();
    }

    /// <summary>One item PER *.avd directory; risk is Review (stale 30+ days) or InUse — never Low.</summary>
    private void ScanAvds(List<StorageItem> items, string home, CancellationToken ct)
    {
        try
        {
            var avdRoot = Path.Combine(home, ".android", "avd");
            if (!Directory.Exists(avdRoot))
                return;

            var avdManager = ResolveAvdManager();

            foreach (var avdDirectory in Directory.GetDirectories(avdRoot, "*.avd"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var size = DirectoryMetrics.GetSizeBytes(avdDirectory, ct);
                    if (size == 0)
                        continue;

                    var name = Path.GetFileNameWithoutExtension(avdDirectory);
                    var lastUsedUtc = Directory.GetLastWriteTimeUtc(avdDirectory);
                    var risk = DateTime.UtcNow - lastUsedUtc > AvdStaleAge ? RiskLevel.Review : RiskLevel.InUse;

                    items.Add(new StorageItem("Android AVDs", name, avdDirectory, size, risk, lastUsedUtc,
                        avdManager, avdManager is null ? null : $"delete avd -n {name}"));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    // Fault isolation contract: one unreadable AVD never sinks the scan.
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // Fault isolation contract: an unreadable AVD root never sinks the scan.
        }
    }

    /// <summary>One item per top-level directory under &lt;sdk&gt;/system-images (e.g. android-34).</summary>
    private void ScanSystemImages(List<StorageItem> items, CancellationToken ct)
    {
        try
        {
            var systemImagesRoot = Path.Combine(_platform.DefaultAndroidSdkPath, "system-images");
            if (!Directory.Exists(systemImagesRoot))
                return;

            foreach (var imageDirectory in Directory.GetDirectories(systemImagesRoot))
            {
                ct.ThrowIfCancellationRequested();
                AddDirectoryItem(items, "Android system images", Path.GetFileName(imageDirectory),
                    imageDirectory, RiskLevel.Review, null, null, ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // Fault isolation contract: an unreadable SDK root never sinks the scan.
        }
    }

    private static void AddDirectoryItem(
        List<StorageItem> items,
        string category,
        string name,
        string path,
        RiskLevel risk,
        string? deleteToolExecutable,
        string? deleteToolArguments,
        CancellationToken ct)
    {
        try
        {
            if (!Directory.Exists(path))
                return;

            var size = DirectoryMetrics.GetSizeBytes(path, ct);
            if (size == 0)
                return; // Nothing reclaimable — omit the category entirely.

            items.Add(new StorageItem(category, name, path, size, risk,
                Directory.GetLastWriteTimeUtc(path), deleteToolExecutable, deleteToolArguments));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // Fault isolation contract (see class doc): one failing category never kills the scan.
        }
    }

    /// <summary>
    /// avdmanager lives at &lt;sdk&gt;/cmdline-tools/latest/bin/avdmanager(.bat); PATH is the fallback.
    /// Null means CleanupService will use the manual-walk fallback (which also removes the .ini).
    /// </summary>
    private string? ResolveAvdManager()
    {
        var candidate = Path.Combine(_platform.DefaultAndroidSdkPath, "cmdline-tools", "latest", "bin",
            _platform.IsWindows ? "avdmanager.bat" : "avdmanager");
        return File.Exists(candidate) ? candidate : _platform.LocateExecutable("avdmanager");
    }
}
