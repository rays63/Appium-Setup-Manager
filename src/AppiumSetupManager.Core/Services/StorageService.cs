using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface IStorageService
{
    Task<IReadOnlyList<StorageItem>> ScanAsync(CancellationToken ct = default);
}

public sealed class StorageService(IPlatformAdapter platform) : IStorageService
{
    public async Task<IReadOnlyList<StorageItem>> ScanAsync(CancellationToken ct = default)
    {
        // TODO: implement category discovery (Phase 5)
        await Task.CompletedTask;
        return [];
    }
}
