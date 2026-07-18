namespace AppiumSetupManager.Core.Infrastructure;

/// <summary>
/// Crash-safe file writes shared by the JSON stores: content is written to a GUID-named temp
/// file in the same directory, then moved over the final path in one step, so a process crash
/// mid-write never leaves a half-written (corrupt) file behind. Creates the parent directory
/// if it does not exist yet.
/// </summary>
public static class AtomicFile
{
    public static async Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default)
    {
        var tempPath = PrepareTempPath(path);
        await File.WriteAllTextAsync(tempPath, contents, ct).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }

    public static void WriteAllText(string path, string contents)
    {
        var tempPath = PrepareTempPath(path);
        File.WriteAllText(tempPath, contents);
        File.Move(tempPath, path, overwrite: true);
    }

    private static string PrepareTempPath(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
    }
}
