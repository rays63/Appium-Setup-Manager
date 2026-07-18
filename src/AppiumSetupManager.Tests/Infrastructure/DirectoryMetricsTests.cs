using AppiumSetupManager.Core.Infrastructure;
using FluentAssertions;
using Xunit;

namespace AppiumSetupManager.Tests.Infrastructure;

public class DirectoryMetricsTests : IDisposable
{
    private readonly string _root;

    public DirectoryMetricsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    private string CreateFile(string relativePath, int sizeBytes)
    {
        var fullPath = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, new byte[sizeBytes]);
        return fullPath;
    }

    [Fact]
    public void GetSizeBytes_SumsExactByteTotalsAcrossNestedDirectories()
    {
        CreateFile("a.bin", 100);
        CreateFile(Path.Combine("sub", "b.bin"), 2048);
        CreateFile(Path.Combine("sub", "deep", "c.bin"), 7);

        var size = DirectoryMetrics.GetSizeBytes(_root);

        size.Should().Be(100 + 2048 + 7);
    }

    [Fact]
    public void GetSizeBytes_MissingPath_ReturnsZero()
    {
        var size = DirectoryMetrics.GetSizeBytes(Path.Combine(_root, "does-not-exist"));

        size.Should().Be(0);
    }

    [Fact]
    public void GetSizeBytes_SkipsSymlinksInsteadOfFollowingThem()
    {
        if (OperatingSystem.IsWindows())
            return; // Symlink creation needs elevation on Windows; behaviour is unix-verified.

        // Target OUTSIDE the scanned tree, holding real bytes.
        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(target);
        try
        {
            File.WriteAllBytes(Path.Combine(target, "big.bin"), new byte[4096]);
            CreateFile("real.bin", 10);
            Directory.CreateSymbolicLink(Path.Combine(_root, "dir-link"), target);
            File.CreateSymbolicLink(Path.Combine(_root, "file-link"), Path.Combine(target, "big.bin"));

            var size = DirectoryMetrics.GetSizeBytes(_root);

            // Only the real file counts; neither symlink is followed or sized.
            size.Should().Be(10);
        }
        finally
        {
            Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void DeleteRecursive_ReturnsMeasuredFreedBytes_AndRemovesTree()
    {
        CreateFile("a.bin", 300);
        CreateFile(Path.Combine("sub", "b.bin"), 1024);

        var (freed, failedEntries) = DirectoryMetrics.DeleteRecursive(_root);

        freed.Should().Be(300 + 1024);
        failedEntries.Should().Be(0);
        Directory.Exists(_root).Should().BeFalse();
    }

    [Fact]
    public void DeleteRecursive_DeletesSymlinkEntryWithoutFollowingIt()
    {
        if (OperatingSystem.IsWindows())
            return; // Symlink creation needs elevation on Windows; behaviour is unix-verified.

        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(target);
        try
        {
            var protectedFile = Path.Combine(target, "keep.bin");
            File.WriteAllBytes(protectedFile, new byte[512]);
            CreateFile("real.bin", 20);
            Directory.CreateSymbolicLink(Path.Combine(_root, "dir-link"), target);

            var (freed, failedEntries) = DirectoryMetrics.DeleteRecursive(_root);

            // The link is removed but its target's contents survive untouched.
            freed.Should().Be(20);
            failedEntries.Should().Be(0);
            Directory.Exists(_root).Should().BeFalse();
            File.Exists(protectedFile).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void DeleteRecursive_PartialFailure_StillDeletesOtherFilesAndCountsFailures()
    {
        if (OperatingSystem.IsWindows())
            return; // Relies on unix directory permissions to force a per-file delete failure.

        var deletable = CreateFile("deletable.bin", 111);
        var locked = CreateFile(Path.Combine("locked", "stuck.bin"), 222);
        var lockedDir = Path.GetDirectoryName(locked)!;

        // Read+execute but no write on the parent dir: the file inside cannot be unlinked.
        File.SetUnixFileMode(lockedDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var (freed, failedEntries) = DirectoryMetrics.DeleteRecursive(_root);

            freed.Should().Be(111);
            failedEntries.Should().BeGreaterThan(0);
            File.Exists(deletable).Should().BeFalse();
            File.Exists(locked).Should().BeTrue();
        }
        finally
        {
            File.SetUnixFileMode(lockedDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void GetSizeBytes_CancelledToken_ThrowsOperationCanceledException()
    {
        CreateFile("a.bin", 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => DirectoryMetrics.GetSizeBytes(_root, cts.Token);

        act.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void DeleteRecursive_CancelledToken_ThrowsOperationCanceledException()
    {
        CreateFile("a.bin", 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => DirectoryMetrics.DeleteRecursive(_root, cts.Token);

        act.Should().Throw<OperationCanceledException>();
        File.Exists(Path.Combine(_root, "a.bin")).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(512, "512 bytes")]
    [InlineData(2048, "2 KB")]
    [InlineData(327_155_712, "312 MB")]
    [InlineData(1_503_238_553, "1.4 GB")]
    public void Humanize_FormatsBytesForDisplay(long bytes, string expected)
    {
        DirectoryMetrics.Humanize(bytes).Should().Be(expected);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
