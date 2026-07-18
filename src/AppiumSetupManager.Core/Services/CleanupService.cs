using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Core.Services;

public interface ICleanupService
{
    Task<long> DeleteAsync(IEnumerable<StorageItem> items, CancellationToken ct = default);
}

public sealed class CleanupService : ICleanupService
{
    private readonly ICommandRunner _runner;
    private readonly IHistoryService _history;

    public CleanupService(ICommandRunner runner, IHistoryService history)
    {
        _runner = runner;
        _history = history;
    }

    public async Task<long> DeleteAsync(IEnumerable<StorageItem> items, CancellationToken ct = default)
    {
        // TODO: execute delete commands per item (Phase 5)
        await Task.CompletedTask;
        var freedBytes = 0L;

        // NOTE: freedBytes is always 0 until the deletion-stub TODO above is implemented — this is a
        // pre-existing gap, not a bug introduced by history logging. The summary will read
        // "Freed 0 bytes" until then.
        await _history.RecordAsync(HistoryEntryType.Cleanup, "Cleaned up storage", $"Freed {freedBytes} bytes", null, ct)
            .ConfigureAwait(false);

        return freedBytes;
    }
}
