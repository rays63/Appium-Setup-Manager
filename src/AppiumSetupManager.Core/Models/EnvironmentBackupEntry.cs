namespace AppiumSetupManager.Core.Models;

/// <summary>
/// Records the previous value of an environment variable immediately before this app changed it,
/// so a user can see what was overwritten (and, in future, restore it) from the Environment screen.
/// </summary>
public record EnvironmentBackupEntry(
    string Id,
    string VariableName,
    string PreviousValue,
    DateTimeOffset CreatedAtUtc,
    string Reason
);
