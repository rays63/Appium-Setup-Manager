using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using FluentAssertions;
using NSubstitute;

namespace AppiumSetupManager.Tests.Infrastructure;

public class EnvironmentVariableManagerTests : IDisposable
{
    private readonly string _tempHome;
    private readonly IPlatformAdapter _platform;
    private readonly EnvironmentVariableManager _sut;

    public EnvironmentVariableManagerTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempHome);

        _platform = Substitute.For<IPlatformAdapter>();
        _platform.HomeDirectory.Returns(_tempHome);
        _platform.IsWindows.Returns(false);

        _sut = new EnvironmentVariableManager(_platform);
    }

    [Fact]
    public async Task SetUserAsync_Unix_AppendsExportToRcFile()
    {
        Environment.SetEnvironmentVariable("SHELL", "/bin/zsh");
        var rcFile = Path.Combine(_tempHome, ".zshrc");

        await _sut.SetUserAsync("TEST_VAR", "/some/path");

        var content = await File.ReadAllTextAsync(rcFile);
        content.Should().Contain("export TEST_VAR=\"/some/path\"");
    }

    [Fact]
    public async Task SetUserAsync_Unix_IsIdempotent()
    {
        Environment.SetEnvironmentVariable("SHELL", "/bin/zsh");
        var rcFile = Path.Combine(_tempHome, ".zshrc");

        await _sut.SetUserAsync("TEST_VAR", "/some/path");
        await _sut.SetUserAsync("TEST_VAR", "/some/path");

        var content = await File.ReadAllTextAsync(rcFile);
        var occurrences = content.Split("export TEST_VAR=").Length - 1;
        occurrences.Should().Be(1);
    }

    [Fact]
    public async Task AppendToPathAsync_Unix_AppendsPathExport()
    {
        Environment.SetEnvironmentVariable("SHELL", "/bin/bash");
        var rcFile = Path.Combine(_tempHome, ".bashrc");

        await _sut.AppendToPathAsync("/usr/local/android/platform-tools");

        var content = await File.ReadAllTextAsync(rcFile);
        content.Should().Contain("/usr/local/android/platform-tools");
    }

    [Fact]
    public async Task AppendToPathAsync_Unix_IsIdempotent()
    {
        Environment.SetEnvironmentVariable("SHELL", "/bin/bash");
        var rcFile = Path.Combine(_tempHome, ".bashrc");

        await _sut.AppendToPathAsync("/usr/local/android/platform-tools");
        await _sut.AppendToPathAsync("/usr/local/android/platform-tools");

        var content = await File.ReadAllTextAsync(rcFile);
        var occurrences = content.Split("/usr/local/android/platform-tools").Length - 1;
        occurrences.Should().Be(1);
    }

    [Fact]
    public void Get_ReturnsEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable("ASM_TEST_GET", "hello");
        _sut.Get("ASM_TEST_GET").Should().Be("hello");
        Environment.SetEnvironmentVariable("ASM_TEST_GET", null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempHome))
            Directory.Delete(_tempHome, recursive: true);
    }
}
