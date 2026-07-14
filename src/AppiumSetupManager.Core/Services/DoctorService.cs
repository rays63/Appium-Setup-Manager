using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Core.Services;

public interface IDoctorService
{
    Task<IReadOnlyList<DoctorCheck>> RunChecksAsync(CancellationToken ct = default);
}

public sealed class DoctorService(ICommandRunner runner) : IDoctorService
{
    public async Task<IReadOnlyList<DoctorCheck>> RunChecksAsync(CancellationToken ct = default)
    {
        // TODO: implement all health checks (Phase 4)
        await Task.CompletedTask;
        return [];
    }
}
