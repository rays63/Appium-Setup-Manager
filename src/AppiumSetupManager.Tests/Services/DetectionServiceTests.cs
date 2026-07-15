using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Services;

public class DetectionServiceTests
{
    // ── Factories ────────────────────────────────────────────────────────────

    private static ICommandRunner CreateRunner() => Substitute.For<ICommandRunner>();

    private static IPlatformAdapter MacPlatform()
    {
        var p = Substitute.For<IPlatformAdapter>();
        p.IsMacOs.Returns(true);
        p.IsWindows.Returns(false);
        p.IsLinux.Returns(false);
        p.HomeDirectory.Returns("/Users/testuser");
        return p;
    }

    private static IPlatformAdapter NonMacPlatform()
    {
        var p = Substitute.For<IPlatformAdapter>();
        p.IsMacOs.Returns(false);
        p.IsWindows.Returns(false);
        p.IsLinux.Returns(true);
        p.HomeDirectory.Returns("/home/testuser");
        return p;
    }

    private static CommandResult OkResult(string stdOut = "", string stdErr = "") =>
        new("cmd args", 0, stdOut, stdErr, TimedOut: false);

    private static CommandResult TimedOutResult() =>
        new("cmd args", -1, string.Empty, "Timed out.", TimedOut: true);

    private static CommandResult FailResult(string stdErr = "") =>
        new("cmd args", 1, string.Empty, stdErr, TimedOut: false);

    // ── Helper: wire all runners to return quickly with harmless output ─────

    private static void WireAllInstantMocks(ICommandRunner runner, IPlatformAdapter platform)
    {
        // node --version
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("v20.11.0"));

        // npm --version
        runner.RunAsync("npm", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("10.2.4"));

        // java -version
        runner.RunAsync("java", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult(stdOut: "", stdErr: "openjdk version \"21.0.3\" 2024-01-16"));

        // adb version
        runner.RunAsync("adb", "version", Arg.Any<CancellationToken>())
              .Returns(OkResult("Android Debug Bridge version 35.0.2"));

        // xcodebuild -version (only called on macOS)
        runner.RunAsync("xcodebuild", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult("Xcode 15.2\nBuild version 15C500b"));

        // xcode-select -p (only called on macOS)
        runner.RunAsync("xcode-select", "-p", Arg.Any<CancellationToken>())
              .Returns(OkResult("/Library/Developer/CommandLineTools"));

        // appium --version
        runner.RunAsync("appium", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("2.5.4"));

        // appium driver list --installed
        runner.RunAsync("appium", "driver list --installed", Arg.Any<CancellationToken>())
              .Returns(OkResult("uiautomator2\nxcuitest"));
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProbeNode_KnownGoodOutput_ReturnsFound()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("v20.11.0"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var node = results.Single(r => r.Name == "Node.js");
        node.State.Should().Be(DetectionState.Found);
        node.InstalledVersion.Should().Be("v20.11.0");
    }

    [Fact]
    public async Task ProbeNode_OldVersion_ReturnsOutdated()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("v16.3.1"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var node = results.Single(r => r.Name == "Node.js");
        node.State.Should().Be(DetectionState.Outdated);
        node.InstalledVersion.Should().Be("v16.3.1");
    }

    [Fact]
    public async Task ProbeNode_TimedOut_ReturnsNotFound()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(TimedOutResult());

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var node = results.Single(r => r.Name == "Node.js");
        node.State.Should().Be(DetectionState.NotFound);
    }

    [Fact]
    public async Task ProbeJdk_ParsesFromStdErr()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("java", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult(stdOut: "", stdErr: "openjdk version \"21.0.3\" 2024"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var jdk = results.Single(r => r.Name == "JDK");
        jdk.State.Should().Be(DetectionState.Found);
        jdk.InstalledVersion.Should().Contain("21");
    }

    [Fact]
    public async Task ProbeJdk_OracleFormat_ParsesCorrectly()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("java", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult(stdOut: "", stdErr: "java version \"11.0.18\" 2023-01-17 LTS"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var jdk = results.Single(r => r.Name == "JDK");
        jdk.State.Should().Be(DetectionState.Found);
        jdk.InstalledVersion.Should().Contain("11");
    }

    [Fact]
    public async Task ProbeXcode_NonMacOs_ReturnsNotApplicable()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var xcode = results.Single(r => r.Name == "Xcode");
        xcode.State.Should().Be(DetectionState.NotApplicable);

        // xcodebuild must not have been called on a non-macOS platform.
        await runner.DidNotReceive().RunAsync("xcodebuild", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProbeDrivers_BothPresent()
    {
        var runner = CreateRunner();
        var platform = MacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("appium", "driver list --installed", Arg.Any<CancellationToken>())
              .Returns(OkResult("uiautomator2\nxcuitest"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        results.Single(r => r.Name == "UiAutomator2 Driver").State.Should().Be(DetectionState.Found);
        results.Single(r => r.Name == "XCUITest Driver").State.Should().Be(DetectionState.Found);
    }

    [Fact]
    public async Task ProbeDrivers_XcuiTest_NonMacOs_ReturnsNotApplicable()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        // Even though the output contains "xcuitest", non-macOS should return NotApplicable.
        runner.RunAsync("appium", "driver list --installed", Arg.Any<CancellationToken>())
              .Returns(OkResult("uiautomator2\nxcuitest"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        results.Single(r => r.Name == "XCUITest Driver").State.Should().Be(DetectionState.NotApplicable);
    }

    [Fact]
    public async Task ScanAllAsync_CompletesUnder10Seconds()
    {
        var runner = CreateRunner();
        var platform = MacPlatform();
        WireAllInstantMocks(runner, platform);

        var sut = new DetectionService(runner, platform);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = await sut.ScanAllAsync();
        stopwatch.Stop();

        results.Should().HaveCount(12);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }
}
