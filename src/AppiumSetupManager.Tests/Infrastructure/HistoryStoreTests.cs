using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Infrastructure;

public class HistoryStoreTests : IDisposable
{
    private readonly string _tempHome;
    private readonly IPlatformAdapter _platform;
    private readonly HistoryStore _sut;

    public HistoryStoreTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempHome);

        _platform = Substitute.For<IPlatformAdapter>();
        _platform.HomeDirectory.Returns(_tempHome);

        _sut = new HistoryStore(_platform);
    }

    private string HistoryFilePath => Path.Combine(_tempHome, ".appiumsetupmanager", "history.json");

    [Fact]
    public async Task RecordAsync_ThenListAsync_RoundTripsEntry()
    {
        await _sut.RecordAsync(HistoryEntryType.Install, "Installed Appium", "npm install -g appium", null);

        var entries = await _sut.ListAsync();

        entries.Should().ContainSingle();
        var entry = entries[0];
        entry.Type.Should().Be(HistoryEntryType.Install);
        entry.Title.Should().Be("Installed Appium");
        entry.Summary.Should().Be("npm install -g appium");
        entry.RollbackTargetId.Should().BeNull();
        entry.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestFirst()
    {
        // Ordering convention: ListAsync returns entries newest-first (descending by CreatedAtUtc),
        // so the most recent action is always at the top of the list for display.
        await _sut.RecordAsync(HistoryEntryType.Install, "first");
        await Task.Delay(10);
        await _sut.RecordAsync(HistoryEntryType.Update, "second");
        await Task.Delay(10);
        await _sut.RecordAsync(HistoryEntryType.Repair, "third");

        var entries = await _sut.ListAsync();

        entries.Should().HaveCount(3);
        entries.Select(e => e.Title).Should().ContainInOrder("third", "second", "first");
    }

    [Fact]
    public async Task RecordAsync_BeyondCap_DropsOldestEntriesNotNewest()
    {
        for (var i = 0; i < 505; i++)
        {
            await _sut.RecordAsync(HistoryEntryType.Install, $"entry-{i}");
        }

        var entries = await _sut.ListAsync();

        entries.Should().HaveCount(500);
        // The newest 500 (entry-5 .. entry-504) must survive; the oldest 5 (entry-0 .. entry-4) must
        // be dropped.
        entries.Select(e => e.Title).Should().Contain("entry-504");
        entries.Select(e => e.Title).Should().NotContain(["entry-0", "entry-1", "entry-2", "entry-3", "entry-4"]);
    }

    [Fact]
    public async Task ListAsync_CorruptFile_ReturnsEmptyListInsteadOfThrowing()
    {
        var directory = Path.GetDirectoryName(HistoryFilePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(HistoryFilePath, "{ this is not valid json !!! ");

        var entries = await _sut.ListAsync();

        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_MissingFile_ReturnsEmptyListWithoutException()
    {
        File.Exists(HistoryFilePath).Should().BeFalse();

        var entries = await _sut.ListAsync();

        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_SetsRollbackTargetId_WhenProvided()
    {
        await _sut.RecordAsync(HistoryEntryType.Setup, "Updated JAVA_HOME", "/new/jdk", "backup-id-123");

        var entries = await _sut.ListAsync();

        entries.Should().ContainSingle();
        entries[0].RollbackTargetId.Should().Be("backup-id-123");
        entries[0].IsRollbackCapable.Should().BeTrue();
    }

    [Fact]
    public async Task NonSetupEntry_IsNeverRollbackCapable_EvenIfRollbackTargetIdIsSet()
    {
        // Only Setup entries can be rollback-capable — this is enforced by IsRollbackCapable itself,
        // but assert it here so a future change to that logic is caught by this test.
        await _sut.RecordAsync(HistoryEntryType.Cleanup, "Cleaned up storage", "Freed 0 bytes", "some-id");

        var entries = await _sut.ListAsync();

        entries[0].IsRollbackCapable.Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempHome))
            Directory.Delete(_tempHome, recursive: true);
    }
}
