using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;

namespace AppiumSetupManager.Core.Services;

public interface IInstallerService
{
    IAsyncEnumerable<InstallStep> InstallAllAsync(CancellationToken ct = default);
    IAsyncEnumerable<InstallStep> InstallSelectedAsync(IEnumerable<string> componentNames, CancellationToken ct = default);
}

public sealed class InstallerService(ICommandRunner runner, IPlatformAdapter platform) : IInstallerService
{
    public async IAsyncEnumerable<InstallStep> InstallAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // TODO: implement ordered install steps (Phase 3)
        await Task.CompletedTask;
        yield break;
    }

    public async IAsyncEnumerable<InstallStep> InstallSelectedAsync(IEnumerable<string> componentNames, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // TODO: implement selective install (Phase 3)
        await Task.CompletedTask;
        yield break;
    }
}
