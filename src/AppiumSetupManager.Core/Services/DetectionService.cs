using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface IDetectionService
{
    Task<IReadOnlyList<ComponentStatus>> ScanAllAsync(CancellationToken ct = default);
}

public sealed class DetectionService(ICommandRunner runner, IPlatformAdapter platform) : IDetectionService
{
    public async Task<IReadOnlyList<ComponentStatus>> ScanAllAsync(CancellationToken ct = default)
    {
        // TODO: implement per-component probes (Phase 2)
        await Task.CompletedTask;
        return [];
    }
}
