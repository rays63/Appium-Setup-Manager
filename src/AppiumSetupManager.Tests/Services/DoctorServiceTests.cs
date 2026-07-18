using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Services;

public class DoctorServiceTests
{
    // ── Factories ────────────────────────────────────────────────────────────

    private static CommandResult OkResult(string stdOut = "", string stdErr = "") =>
        new("cmd", 0, stdOut, stdErr, false);

    private static CommandResult FailResult(string stdErr = "") =>
        new("cmd", 1, "", stdErr, false);

    private static CommandResult TimedOutResult() =>
        new("cmd", -1, "", "", true);

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

    private static DoctorService CreateService(ICommandRunner? runner = null, IPlatformAdapter? platform = null, IHistoryService? history = null)
    {
        runner ??= CreateDefaultRunner();
        platform ??= MacPlatform();
        history ??= Substitute.For<IHistoryService>();
        return new DoctorService(runner, platform, history);
    }

    private static ICommandRunner CreateDefaultRunner()
    {
        var runner = Substitute.For<ICommandRunner>();
        // Wire all commands to return harmless empty-success results by default
        runner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(OkResult());
        return runner;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckNode_GoodVersion_ReturnsPass()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("v20.11.0"));

        var sut = CreateService(runner, NonMacPlatform());
        var results = await sut.RunChecksAsync();

        var node = results.Single(c => c.Name == "Node.js");
        node.Result.Should().Be(CheckResult.Pass);
    }

    [Fact]
    public async Task CheckNode_OldVersion_ReturnsWarn()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("v16.3.1"));

        var sut = CreateService(runner, NonMacPlatform());
        var results = await sut.RunChecksAsync();

        var node = results.Single(c => c.Name == "Node.js");
        node.Result.Should().Be(CheckResult.Warn);
    }

    [Fact]
    public async Task CheckNode_NotFound_ReturnsFail()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(FailResult());

        var sut = CreateService(runner, NonMacPlatform());
        var results = await sut.RunChecksAsync();

        var node = results.Single(c => c.Name == "Node.js");
        node.Result.Should().Be(CheckResult.Fail);
    }

    [Fact]
    public async Task CheckJdk_ParsesStdErr()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("java", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult(stdOut: "", stdErr: "openjdk version \"21.0.3\" 2024"));

        var sut = CreateService(runner, NonMacPlatform());
        var results = await sut.RunChecksAsync();

        var jdk = results.Single(c => c.Name == "JDK");
        jdk.Result.Should().Be(CheckResult.Pass);
        jdk.Description.Should().Contain("21");
    }

    [Fact]
    public async Task CheckJdk_OldVersion_ReturnsWarn()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("java", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult(stdOut: "", stdErr: "java version \"8.0.301\""));

        var sut = CreateService(runner, NonMacPlatform());
        var results = await sut.RunChecksAsync();

        var jdk = results.Single(c => c.Name == "JDK");
        jdk.Result.Should().Be(CheckResult.Warn);
    }

    [Fact]
    public async Task CheckXcuiTest_NonMacOs_NotInResults()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("appium", "driver list --installed", Arg.Any<CancellationToken>())
              .Returns(OkResult("uiautomator2\nxcuitest"));

        var sut = CreateService(runner, NonMacPlatform());
        var results = await sut.RunChecksAsync();

        results.Should().NotContain(c => c.Name == "XCUITest Driver");
    }

    [Fact]
    public async Task CheckXcuiTest_MacOs_DriverPresent_ReturnsPass()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("appium", "driver list --installed", Arg.Any<CancellationToken>())
              .Returns(OkResult("uiautomator2\nxcuitest"));

        var sut = CreateService(runner, MacPlatform());
        var results = await sut.RunChecksAsync();

        var xcui = results.Single(c => c.Name == "XCUITest Driver");
        xcui.Result.Should().Be(CheckResult.Pass);
    }

    [Fact]
    public async Task CheckAppium_CanAutoFix_True()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("appium", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("2.5.4"));

        var sut = CreateService(runner, NonMacPlatform());
        var results = await sut.RunChecksAsync();

        var appium = results.Single(c => c.Name == "Appium");
        appium.RemediationCommand.Should().Be("npm install -g appium");
        appium.CanAutoFix.Should().BeTrue();
    }

    [Fact]
    public async Task CheckUiAutomator2_NotInstalled_ReturnsFail()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("appium", "driver list --installed", Arg.Any<CancellationToken>())
              .Returns(OkResult(""));

        var sut = CreateService(runner, NonMacPlatform());
        var results = await sut.RunChecksAsync();

        var ua2 = results.Single(c => c.Name == "UiAutomator2 Driver");
        ua2.Result.Should().Be(CheckResult.Fail);
    }

    [Fact]
    public async Task RunChecksAsync_ReturnsStableOrder()
    {
        var runner = CreateDefaultRunner();
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("v20.11.0"));
        runner.RunAsync("npm", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("10.2.4"));
        runner.RunAsync("java", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult(stdOut: "", stdErr: "openjdk version \"21.0.3\" 2024"));
        runner.RunAsync("adb", "version", Arg.Any<CancellationToken>())
              .Returns(OkResult("Android Debug Bridge version 35.0.2"));
        runner.RunAsync("appium", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("2.5.4"));
        runner.RunAsync("appium", "driver list --installed", Arg.Any<CancellationToken>())
              .Returns(OkResult("uiautomator2\nxcuitest"));

        var sut = CreateService(runner, MacPlatform());
        var results = await sut.RunChecksAsync();

        // All "Dependencies" checks must come before any "Drivers" check
        var groups = results.Select(c => c.Group).ToList();
        var lastDependencyIndex = groups.Select((g, i) => (g, i))
                                        .Where(x => x.g == "Dependencies")
                                        .Select(x => x.i)
                                        .DefaultIfEmpty(-1)
                                        .Max();
        var firstDriverIndex = groups.Select((g, i) => (g, i))
                                     .Where(x => x.g == "Drivers")
                                     .Select(x => x.i)
                                     .DefaultIfEmpty(int.MaxValue)
                                     .Min();

        lastDependencyIndex.Should().BeLessThan(firstDriverIndex);
    }

    [Fact]
    public async Task FixAndRecheck_Appium_RunsRemediationCommandThenRechecks()
    {
        var runner = Substitute.For<ICommandRunner>();

        // Simplest possible setup: every RunAsync call returns a 2.5.0 success result.
        runner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(Task.FromResult(OkResult(stdOut: "2.5.0")));

        var sut = CreateService(runner, MacPlatform());

        var result = await sut.FixAndRecheckAsync("Appium");

        result.Name.Should().Be("Appium");
        result.Result.Should().Be(CheckResult.Pass);
        await runner.Received(1).RunAsync("npm", "install -g appium", Arg.Any<CancellationToken>());
    }
}
