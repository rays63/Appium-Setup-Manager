namespace AppiumSetupManager.Core.Models;

public enum InstallStepState { Pending, Running, Done, Failed, Skipped }

public record InstallStep(
    string ComponentName,
    string Command,
    InstallStepState State,
    string? ErrorMessage,
    string? SuggestedFix
);
