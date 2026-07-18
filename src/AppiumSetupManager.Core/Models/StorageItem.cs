namespace AppiumSetupManager.Core.Models;

/// <summary>
/// One reclaimable disk item found by the storage scan. <paramref name="DeleteToolExecutable"/> /
/// <paramref name="DeleteToolArguments"/> describe an optional CLI-backed deletion (e.g.
/// <c>npm cache clean --force</c>); when the executable is null the item is deleted by a manual
/// recursive walk. CleanupService only ever runs whitelisted executables — a malformed item can
/// never trigger an arbitrary command.
/// </summary>
public record StorageItem(
    string Category,
    string Name,
    string Path,
    long SizeBytes,
    RiskLevel Risk,
    DateTime? LastUsedUtc,
    string? DeleteToolExecutable,
    string? DeleteToolArguments
)
{
    /// <summary>Only genuinely low-risk caches are safe to pre-select for deletion.</summary>
    public bool IsSafeToDelete => Risk == RiskLevel.Low;
}
