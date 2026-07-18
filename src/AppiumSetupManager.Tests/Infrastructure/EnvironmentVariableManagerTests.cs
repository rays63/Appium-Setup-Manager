using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using FluentAssertions;
using NSubstitute;
using Xunit;

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
    public async Task SetUserAsync_UpdatesCurrentProcessEnvironmentImmediately()
    {
        // Regression test for a real bug: SetUserAsync only ever wrote the persisted value (shell rc
        // file / registry), which takes effect for *new* processes/shells only. This app's own
        // already-running process never saw the change, so an immediate re-scan (e.g. right after
        // clicking "Detect & Set" for JAVA_HOME) would still read the stale value and look like
        // nothing happened.
        Environment.SetEnvironmentVariable("SHELL", "/bin/zsh");
        Environment.SetEnvironmentVariable("ASM_TEST_LIVE_VAR", null);

        await _sut.SetUserAsync("ASM_TEST_LIVE_VAR", "/some/path");

        Environment.GetEnvironmentVariable("ASM_TEST_LIVE_VAR").Should().Be("/some/path");
        Environment.SetEnvironmentVariable("ASM_TEST_LIVE_VAR", null);
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
    public async Task AppendToPathAsync_UpdatesCurrentProcessPathImmediately()
    {
        Environment.SetEnvironmentVariable("SHELL", "/bin/bash");
        const string newDir = "/asm-test/does-not-need-to-exist/platform-tools";
        var originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        try
        {
            await _sut.AppendToPathAsync(newDir);

            Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator).Should().Contain(newDir);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
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

    // ── TryConfigureJavaHomeAsync ────────────────────────────────────────────

    [Fact]
    public async Task TryConfigureJavaHomeAsync_FindsJdkNestedUnderSearchPath_SetsJavaHome()
    {
        Environment.SetEnvironmentVariable("SHELL", "/bin/zsh");

        // Mirrors macOS's real layout: <searchRoot>/temurin-21.jdk/Contents/Home/bin/java
        var searchRoot = Path.Combine(_tempHome, "JavaVirtualMachines");
        var jdkHome = Path.Combine(searchRoot, "temurin-21.jdk", "Contents", "Home");
        Directory.CreateDirectory(Path.Combine(jdkHome, "bin"));
        await File.WriteAllTextAsync(Path.Combine(jdkHome, "bin", "java"), "#!/bin/sh");
        _platform.DefaultJdkSearchPath.Returns(searchRoot);

        var resolved = await _sut.TryConfigureJavaHomeAsync();

        resolved.Should().Be(jdkHome);
        var content = await File.ReadAllTextAsync(Path.Combine(_tempHome, ".zshrc"));
        content.Should().Contain($"export JAVA_HOME=\"{jdkHome}\"");
    }

    [Fact]
    public async Task TryConfigureJavaHomeAsync_NoJdkPresent_ReturnsNullAndDoesNotWrite()
    {
        var searchRoot = Path.Combine(_tempHome, "JavaVirtualMachines");
        Directory.CreateDirectory(searchRoot);
        _platform.DefaultJdkSearchPath.Returns(searchRoot);

        var resolved = await _sut.TryConfigureJavaHomeAsync();

        resolved.Should().BeNull();
        File.Exists(Path.Combine(_tempHome, ".zshrc")).Should().BeFalse();
    }

    [Fact]
    public async Task TryConfigureJavaHomeAsync_SearchPathMissing_ReturnsNull()
    {
        _platform.DefaultJdkSearchPath.Returns(Path.Combine(_tempHome, "does-not-exist"));

        var resolved = await _sut.TryConfigureJavaHomeAsync();

        resolved.Should().BeNull();
    }

    // ── TryConfigureAndroidHomeAsync ─────────────────────────────────────────

    [Fact]
    public async Task TryConfigureAndroidHomeAsync_SdkPresent_SetsAndroidHomeAndAppendsPath()
    {
        Environment.SetEnvironmentVariable("SHELL", "/bin/zsh");

        var sdkHome = Path.Combine(_tempHome, "Android", "sdk");
        Directory.CreateDirectory(Path.Combine(sdkHome, "platform-tools"));
        _platform.DefaultAndroidSdkPath.Returns(sdkHome);

        var resolved = await _sut.TryConfigureAndroidHomeAsync();

        resolved.Should().Be(sdkHome);
        var content = await File.ReadAllTextAsync(Path.Combine(_tempHome, ".zshrc"));
        content.Should().Contain($"export ANDROID_HOME=\"{sdkHome}\"");
        content.Should().Contain(Path.Combine(sdkHome, "platform-tools"));
    }

    [Fact]
    public async Task TryConfigureAndroidHomeAsync_NoSdkPresent_ReturnsNull()
    {
        _platform.DefaultAndroidSdkPath.Returns(Path.Combine(_tempHome, "Android", "sdk"));

        var resolved = await _sut.TryConfigureAndroidHomeAsync();

        resolved.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempHome))
            Directory.Delete(_tempHome, recursive: true);
    }
}
