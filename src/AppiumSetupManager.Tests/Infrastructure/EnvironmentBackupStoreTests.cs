using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Infrastructure;

public class EnvironmentBackupStoreTests : IDisposable
{
    private readonly string _tempHome;
    private readonly IPlatformAdapter _platform;
    private readonly EnvironmentBackupStore _sut;

    public EnvironmentBackupStoreTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempHome);

        _platform = Substitute.For<IPlatformAdapter>();
        _platform.HomeDirectory.Returns(_tempHome);

        _sut = new EnvironmentBackupStore(_platform);
    }

    private string BackupFilePath => Path.Combine(_tempHome, ".appiumsetupmanager", "env-backups.json");

    [Fact]
    public async Task RecordAsync_ThenListAsync_RoundTripsEntry()
    {
        await _sut.RecordAsync("JAVA_HOME", "/old/jdk", "JDK install overwrote JAVA_HOME");

        var entries = await _sut.ListAsync();

        entries.Should().ContainSingle();
        var entry = entries[0];
        entry.VariableName.Should().Be("JAVA_HOME");
        entry.PreviousValue.Should().Be("/old/jdk");
        entry.Reason.Should().Be("JDK install overwrote JAVA_HOME");
        entry.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestFirst()
    {
        // Ordering convention: ListAsync returns entries newest-first (descending by CreatedAtUtc),
        // so the most recent overwrite is always at the top of the list for display.
        await _sut.RecordAsync("VAR_A", "value-a", "first");
        await Task.Delay(10);
        await _sut.RecordAsync("VAR_B", "value-b", "second");
        await Task.Delay(10);
        await _sut.RecordAsync("VAR_C", "value-c", "third");

        var entries = await _sut.ListAsync();

        entries.Should().HaveCount(3);
        entries.Select(e => e.VariableName).Should().ContainInOrder("VAR_C", "VAR_B", "VAR_A");
    }

    [Fact]
    public async Task RecordAsync_BeyondCap_DropsOldestEntriesNotNewest()
    {
        for (var i = 0; i < 55; i++)
        {
            await _sut.RecordAsync($"VAR_{i}", $"value-{i}", "bulk");
        }

        var entries = await _sut.ListAsync();

        entries.Should().HaveCount(50);
        // The newest 50 (VAR_5 .. VAR_54) must survive; the oldest 5 (VAR_0 .. VAR_4) must be dropped.
        entries.Select(e => e.VariableName).Should().Contain("VAR_54");
        entries.Select(e => e.VariableName).Should().NotContain(["VAR_0", "VAR_1", "VAR_2", "VAR_3", "VAR_4"]);
    }

    [Fact]
    public async Task ListAsync_CorruptFile_ReturnsEmptyListInsteadOfThrowing()
    {
        var directory = Path.GetDirectoryName(BackupFilePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(BackupFilePath, "{ this is not valid json !!! ");

        var entries = await _sut.ListAsync();

        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_MissingFile_ReturnsEmptyListWithoutException()
    {
        File.Exists(BackupFilePath).Should().BeFalse();

        var entries = await _sut.ListAsync();

        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_ThenFindAsync_ReturnsMatchingEntry()
    {
        await _sut.RecordAsync("ANDROID_HOME", "/old/sdk", "SDK install overwrote ANDROID_HOME");
        var entries = await _sut.ListAsync();
        var id = entries[0].Id;

        var found = await _sut.FindAsync(id);

        found.Should().NotBeNull();
        found!.VariableName.Should().Be("ANDROID_HOME");
        found.PreviousValue.Should().Be("/old/sdk");
    }

    [Fact]
    public async Task FindAsync_UnknownId_ReturnsNull()
    {
        await _sut.RecordAsync("PATH", "/old/path", "reason");

        var found = await _sut.FindAsync("does-not-exist");

        found.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempHome))
            Directory.Delete(_tempHome, recursive: true);
    }
}
