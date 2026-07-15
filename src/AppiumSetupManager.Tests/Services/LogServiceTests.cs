using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Services;

public class LogServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IPlatformAdapter _platformMock;
    private readonly LogService _sut;

    public LogServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"logservice-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _platformMock = Substitute.For<IPlatformAdapter>();
        _platformMock.HomeDirectory.Returns(_tempDir);

        _sut = new LogService(_platformMock);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task LogCommand_WritesCommandEntryToChannel()
    {
        _sut.LogCommand("test cmd");

        var entry = await _sut.Entries.ReadAsync();

        entry.Kind.Should().Be(LogEntryKind.Command);
        entry.Text.Should().Contain("test cmd");
    }

    [Fact]
    public async Task LogOutput_StdErr_KindIsStdErr()
    {
        _sut.LogOutput("err line", LogEntryKind.StdErr);

        var entry = await _sut.Entries.ReadAsync();

        entry.Kind.Should().Be(LogEntryKind.StdErr);
        entry.Text.Should().Be("err line");
    }

    [Fact]
    public async Task LogError_WritesErrorKind()
    {
        _sut.LogError("something failed");

        var entry = await _sut.Entries.ReadAsync();

        entry.Kind.Should().Be(LogEntryKind.Error);
        entry.Text.Should().Contain("something failed");
    }

    [Fact]
    public async Task LogError_WithException_IncludesExceptionMessage()
    {
        var ex = new InvalidOperationException("inner problem");

        _sut.LogError("something failed", ex);

        var entry = await _sut.Entries.ReadAsync();

        entry.Kind.Should().Be(LogEntryKind.Error);
        entry.Text.Should().Contain("something failed");
        entry.Text.Should().Contain("inner problem");
    }

    [Fact]
    public void Constructor_CreatesLogDirectory()
    {
        var expectedDir = Path.Combine(_tempDir, ".appiumsetupmanager", "logs");
        Directory.Exists(expectedDir).Should().BeTrue();
    }

    [Fact]
    public async Task LogEntry_HasTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        _sut.LogCommand("timed cmd");
        var after = DateTimeOffset.UtcNow;

        var entry = await _sut.Entries.ReadAsync();

        entry.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
