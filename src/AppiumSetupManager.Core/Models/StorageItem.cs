namespace AppiumSetupManager.Core.Models;

public record StorageItem(
    string Category,
    string Name,
    string Path,
    long SizeBytes,
    bool IsSafeToDelete,
    string DeleteCommand
);
