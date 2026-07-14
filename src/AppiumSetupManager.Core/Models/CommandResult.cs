namespace AppiumSetupManager.Core.Models;

public record CommandResult(
    string Command,
    int ExitCode,
    string StdOut,
    string StdErr,
    bool TimedOut
)
{
    public bool Success => ExitCode == 0 && !TimedOut;
}
