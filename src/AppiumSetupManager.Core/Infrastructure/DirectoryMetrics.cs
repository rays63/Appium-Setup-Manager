using System.Globalization;

namespace AppiumSetupManager.Core.Infrastructure;

/// <summary>
/// Filesystem measurement and deletion primitives shared by StorageService (scan) and
/// CleanupService (delete). Both walks are manual and never follow reparse points, so a symlink
/// planted inside a cache directory can neither inflate the reported size nor cause deletion to
/// escape the directory tree.
/// </summary>
public static class DirectoryMetrics
{
    /// <summary>How many entries are visited between cooperative cancellation checks.</summary>
    private const int CancellationCheckInterval = 256;

    /// <summary>
    /// Sums file sizes under <paramref name="path"/> via a manual recursive walk. Reparse points
    /// (symlinks, junctions) count as 0 bytes and are never descended into. Inaccessible entries
    /// are skipped, so the result is a floor, never an over-count. Returns 0 for a missing path.
    /// </summary>
    public static long GetSizeBytes(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var root = new DirectoryInfo(path);
        if (!root.Exists)
            return 0;

        long total = 0;
        var visited = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var entry in EnumerateEntriesSafe(directory))
            {
                if (++visited % CancellationCheckInterval == 0)
                    ct.ThrowIfCancellationRequested();

                try
                {
                    // Symlinks/junctions: count 0, never follow.
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;

                    if (entry is DirectoryInfo subDirectory)
                        pending.Push(subDirectory);
                    else if (entry is FileInfo file)
                        total += file.Length;
                }
                catch (IOException)
                {
                    // Entry vanished or is unreadable mid-walk — result is a floor by contract.
                }
                catch (UnauthorizedAccessException)
                {
                    // Same: skip and continue.
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Deletes the tree at <paramref name="path"/> bottom-up, file by file. Returns the summed
    /// <see cref="FileInfo.Length"/> of files that were actually deleted (never an assumed total)
    /// plus a count of entries that could not be deleted. Directory reparse points are deleted as
    /// the link entry itself and never descended into. Empty-directory removal failures are
    /// ignored (the files are what hold the bytes).
    /// </summary>
    public static (long FreedBytes, int FailedEntries) DeleteRecursive(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        long freed = 0;
        var failed = 0;

        var root = new DirectoryInfo(path);
        if (!root.Exists)
        {
            // A plain file (or file symlink) passed directly.
            if (File.Exists(path))
                DeleteFile(new FileInfo(path), ref freed, ref failed);
            return (freed, failed);
        }

        if ((root.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            // The path itself is a link: remove the link entry only, freeing 0 measured bytes.
            try { Directory.Delete(path); }
            catch (IOException) { failed++; }
            catch (UnauthorizedAccessException) { failed++; }
            return (freed, failed);
        }

        DeleteDirectoryContents(root, ct, ref freed, ref failed);

        // Finally attempt removing the (now hopefully empty) root itself; failure is ignored.
        try { root.Delete(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return (freed, failed);
    }

    private static void DeleteDirectoryContents(DirectoryInfo directory, CancellationToken ct, ref long freed, ref int failed)
    {
        foreach (var entry in EnumerateEntriesSafe(directory))
        {
            ct.ThrowIfCancellationRequested();

            FileAttributes attributes;
            try
            {
                attributes = entry.Attributes;
            }
            catch (IOException) { failed++; continue; }
            catch (UnauthorizedAccessException) { failed++; continue; }

            var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;

            if (entry is DirectoryInfo subDirectory)
            {
                if (isReparsePoint)
                {
                    // Delete the link entry itself — never descend through it.
                    try { Directory.Delete(subDirectory.FullName); }
                    catch (IOException) { failed++; }
                    catch (UnauthorizedAccessException) { failed++; }
                    continue;
                }

                DeleteDirectoryContents(subDirectory, ct, ref freed, ref failed);

                // Bottom-up removal of the now-empty directory; failures ignored by contract.
                try { subDirectory.Delete(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            else if (entry is FileInfo file)
            {
                if (isReparsePoint)
                {
                    // File symlink: remove the link, count 0 bytes (the target keeps its data).
                    try { file.Delete(); }
                    catch (IOException) { failed++; }
                    catch (UnauthorizedAccessException) { failed++; }
                    continue;
                }

                DeleteFile(file, ref freed, ref failed);
            }
        }
    }

    private static void DeleteFile(FileInfo file, ref long freed, ref int failed)
    {
        try
        {
            var length = file.Length;
            file.Delete();
            freed += length;
        }
        catch (IOException) { failed++; }
        catch (UnauthorizedAccessException) { failed++; }
    }

    private static FileSystemInfo[] EnumerateEntriesSafe(DirectoryInfo directory)
    {
        try
        {
            // AttributesToSkip = None: the default skips Hidden|System, and unix dot-files are
            // mapped to Hidden — a cache walk must see (and delete) those too.
            return directory.GetFileSystemInfos("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.None,
            });
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Formats a byte count for display, e.g. "0 bytes", "312 MB", "1.4 GB".</summary>
    public static string Humanize(long bytes)
    {
        if (bytes <= 0)
            return "0 bytes";
        if (bytes < 1024)
            return $"{bytes} bytes";

        string[] units = ["KB", "MB", "GB", "TB", "PB"];
        double value = bytes;
        var unitIndex = -1;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        // Two-or-three significant digits: "1.4 GB" but "312 MB" (no pointless ".0" at scale).
        var format = value >= 100 ? "0" : "0.#";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {units[unitIndex]}";
    }
}
