namespace AppiumSetupManager.Core.Models;

public enum LogEntryKind { Command, StdOut, StdErr, Error, Info }

public record LogEntry(DateTimeOffset Timestamp, LogEntryKind Kind, string Text);
