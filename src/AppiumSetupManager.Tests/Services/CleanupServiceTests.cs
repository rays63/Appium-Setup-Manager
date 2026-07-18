using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Services;

public class CleanupServiceTests : IDisposable
{
    private readonly string _tempHome;
    private readonly ICommandRunner _runner;
    private readonly IHistoryService _history;
    private readonly IPlatformAdapter _platform;
    private readonly CleanupService _sut;

    public CleanupServiceTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempHome);

        _runner = Substitute.For<ICommandRunner>();
        _history = Substitute.For<IHistoryService>();
        _platform = Substitute.For<IPlatformAdapter>();
        _platform.IsLinux.Returns(false);
        _platform.HomeDirectory.Returns(_tempHome);
        _platform.DefaultAndroidSdkPath.Returns(Path.Combine(_tempHome, "android-sdk"));

        _sut = new CleanupService(_runner, _history, _platform);
    }

    private string CreateDirectoryWithFile(string relativeDir, string fileName, int sizeBytes)
    {
        var directory = Path.Combine(_tempHome, relativeDir);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, fileName), new byte[sizeBytes]);
        return directory;
    }

    private static StorageItem ManualItem(string path, long sizeBytes = 0) =>
        new("Gradle caches", "Gradle build caches", path, sizeBytes, RiskLevel.Low, null, null, null);

    [Fact]
    public async Task DeleteAsync_ManualItem_FreesMeasuredBytesAndRemovesDirectory()
    {
        var target = CreateDirectoryWithFile(Path.Combine(".gradle", "caches"), "modules.bin", 4096);

        // Scan-reported size is deliberately WRONG (999) — the result must be measured, not assumed.
        var freed = await _sut.DeleteAsync([ManualItem(target, sizeBytes: 999)]);

        freed.Should().Be(4096);
        Directory.Exists(target).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RecordsHistoryWithHumanizedSize_UsingCancellationTokenNone()
    {
        var target = CreateDirectoryWithFile(Path.Combine(".npm", "_cacache"), "content.bin", 2048);

        await _sut.DeleteAsync([ManualItem(target)]);

        await _history.Received(1).RecordAsync(
            HistoryEntryType.Cleanup,
            "Cleaned up storage",
            Arg.Is<string>(s => s.Contains("2 KB") && s.Contains("1 item(s)")),
            null,
            CancellationToken.None);
    }

    [Fact]
    public async Task DeleteAsync_PathOutsideAllowedRoots_IsRefusedAndCountedAsFailed()
    {
        var outside = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "cache");
        Directory.CreateDirectory(outside);
        try
        {
            File.WriteAllBytes(Path.Combine(outside, "data.bin"), new byte[100]);

            var freed = await _sut.DeleteAsync([ManualItem(outside)]);

            freed.Should().Be(0);
            Directory.Exists(outside).Should().BeTrue("nothing outside home/SDK may ever be touched");
            await _history.Received(1).RecordAsync(
                HistoryEntryType.Cleanup,
                Arg.Any<string>(),
                Arg.Is<string>(s => s.Contains("1 item(s) failed")),
                null,
                CancellationToken.None);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(outside)!, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteAsync_PathEqualToHomeRoot_IsRefused()
    {
        File.WriteAllBytes(Path.Combine(_tempHome, "canary.bin"), new byte[10]);

        var freed = await _sut.DeleteAsync([ManualItem(_tempHome)]);

        freed.Should().Be(0);
        File.Exists(Path.Combine(_tempHome, "canary.bin")).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_PathOnlyOneSegmentBelowRoot_IsRefused()
    {
        var shallow = CreateDirectoryWithFile(".gradle", "canary.bin", 10);

        var freed = await _sut.DeleteAsync([ManualItem(shallow)]);

        freed.Should().Be(0);
        Directory.Exists(shallow).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_NonWhitelistedExecutable_IsNeverRun_FallsBackToManualWalk()
    {
        // Documented decision: an unknown tool is treated as if the item had no tool at all —
        // the directory is still cleaned via the manual walk, but no command is ever executed.
        var target = CreateDirectoryWithFile(Path.Combine(".gradle", "caches"), "modules.bin", 128);
        var malformed = new StorageItem("Gradle caches", "x", target, 128, RiskLevel.Low, null,
            "rm", "-rf /");

        var freed = await _sut.DeleteAsync([malformed]);

        freed.Should().Be(128);
        Directory.Exists(target).Should().BeFalse();
        await _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default);
    }

    [Fact]
    public async Task DeleteAsync_WhitelistedCliSuccess_RunsToolAndMeasuresBeforeAfterDelta()
    {
        var target = CreateDirectoryWithFile(Path.Combine(".npm", "_cacache"), "content.bin", 4096);
        var item = new StorageItem("npm cache", "npm package cache", target, 4096, RiskLevel.Low, null,
            "npm", "cache clean --force");

        _runner.RunAsync("npm", "cache clean --force", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                // Simulate npm emptying the cache: files gone, directory left behind.
                foreach (var file in Directory.GetFiles(target, "*", SearchOption.AllDirectories))
                    File.Delete(file);
                return Task.FromResult(new CommandResult("npm cache clean --force", 0, "", "", false));
            });

        var freed = await _sut.DeleteAsync([item]);

        freed.Should().Be(4096, "freed bytes must be the measured before/after size delta");
        await _runner.Received(1).RunAsync("npm", "cache clean --force", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_CliFailure_FallsBackToManualWalk()
    {
        var target = CreateDirectoryWithFile(Path.Combine(".npm", "_cacache"), "content.bin", 1024);
        var item = new StorageItem("npm cache", "npm package cache", target, 1024, RiskLevel.Low, null,
            "npm", "cache clean --force");

        _runner.RunAsync("npm", "cache clean --force", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CommandResult("npm cache clean --force", 1, "", "boom", false)));

        var freed = await _sut.DeleteAsync([item]);

        freed.Should().Be(1024);
        Directory.Exists(target).Should().BeFalse("the manual walk must clean up after a CLI failure");
    }

    [Fact]
    public async Task DeleteAsync_AvdManualFallback_AlsoDeletesSiblingIniFile()
    {
        var avdDirectory = CreateDirectoryWithFile(
            Path.Combine(".android", "avd", "Pixel_7.avd"), "userdata.img", 2000);
        var iniPath = Path.Combine(_tempHome, ".android", "avd", "Pixel_7.ini");
        File.WriteAllBytes(iniPath, new byte[50]);

        // No delete tool → manual fallback path (as when avdmanager cannot be located).
        var item = new StorageItem("Android AVDs", "Pixel_7", avdDirectory, 2000, RiskLevel.Review, null, null, null);

        var freed = await _sut.DeleteAsync([item]);

        freed.Should().Be(2000 + 50);
        Directory.Exists(avdDirectory).Should().BeFalse();
        File.Exists(iniPath).Should().BeFalse("a dangling .ini would leave a ghost AVD in avdmanager listings");
    }

    [Fact]
    public async Task DeleteAsync_CancelledMidRun_RecordsPartialHistoryAndPropagatesOce()
    {
        var first = CreateDirectoryWithFile(Path.Combine(".npm", "_cacache"), "content.bin", 2048);
        var second = CreateDirectoryWithFile(Path.Combine(".gradle", "caches"), "modules.bin", 512);
        var firstItem = new StorageItem("npm cache", "npm package cache", first, 2048, RiskLevel.Low, null,
            "npm", "cache clean --force");

        using var cts = new CancellationTokenSource();
        _runner.RunAsync("npm", "cache clean --force", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Directory.Delete(first, recursive: true);
                cts.Cancel(); // Cancellation lands between item 1 and item 2.
                return Task.FromResult(new CommandResult("npm cache clean --force", 0, "", "", false));
            });

        var act = () => _sut.DeleteAsync([firstItem, ManualItem(second)], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.Exists(second).Should().BeTrue("the second item must not be processed after cancellation");
        await _history.Received(1).RecordAsync(
            HistoryEntryType.Cleanup,
            "Cleaned up storage",
            Arg.Is<string>(s => s.Contains("Cancelled") && s.Contains("2 KB")),
            null,
            CancellationToken.None);
    }

    [Fact]
    public async Task DeleteAsync_EmptyList_ReturnsZeroAndRecordsNoHistory()
    {
        var freed = await _sut.DeleteAsync([]);

        freed.Should().Be(0);
        await _history.DidNotReceiveWithAnyArgs().RecordAsync(default, default!, default, default, default);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempHome))
            Directory.Delete(_tempHome, recursive: true);
    }
}
