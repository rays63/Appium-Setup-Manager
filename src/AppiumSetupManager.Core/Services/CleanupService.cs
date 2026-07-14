using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Core.Services;

public interface ICleanupService
{
    Task<long> DeleteAsync(IEnumerable<StorageItem> items, CancellationToken ct = default);
}

public sealed class CleanupService(ICommandRunner runner) : ICleanupService
{
    public async Task<long> DeleteAsync(IEnumerable<StorageItem> items, CancellationToken ct = default)
    {
        // TODO: execute delete commands per item (Phase 5)
        await Task.CompletedTask;
        return 0;
    }
}
