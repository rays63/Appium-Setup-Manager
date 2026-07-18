namespace AppiumSetupManager.Core.Models;

public enum HistoryEntryType { Install, Update, Repair, Cleanup, Setup }

/// <summary>
/// A single log line on the History screen. Only <see cref="HistoryEntryType.Setup"/> entries can
/// ever be rollback-capable, because environment-variable changes are the only history action with
/// a real inverse operation already built (<see cref="IEnvironmentBackupStore"/>). Install, Update,
/// Repair, and Cleanup entries are plain, non-reversible log lines.
/// </summary>
public record HistoryEntry(
    string Id,
    HistoryEntryType Type,
    DateTimeOffset CreatedAtUtc,
    string Title,
    string? Summary,
    string? RollbackTargetId)
{
    public bool IsRollbackCapable => Type == HistoryEntryType.Setup && RollbackTargetId is not null;
}
