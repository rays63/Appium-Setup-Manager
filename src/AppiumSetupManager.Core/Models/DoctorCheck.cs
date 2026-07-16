namespace AppiumSetupManager.Core.Models;

public enum CheckResult { Pass, Warn, Fail }

public record DoctorCheck(
    string Name,
    string Group,
    CheckResult Result,
    string Description,
    string? RemediationCommand,
    bool CanAutoFix
);
