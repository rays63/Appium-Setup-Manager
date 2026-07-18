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
        p.LocateExecutable(Arg.Any<string>()).Returns(ci => $"/usr/local/bin/{ci.Arg<string>()}");
        return p;
    }

    private static IPlatformAdapter NonMacPlatform()
    {
        var p = Substitute.For<IPlatformAdapter>();
        p.IsMacOs.Returns(false);
        p.IsWindows.Returns(false);
        p.IsLinux.Returns(true);
        p.HomeDirectory.Returns("/home/testuser");
        p.LocateExecutable(Arg.Any<string>()).Returns(ci => $"/usr/local/bin/{ci.Arg<string>()}");
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
        // brew --version (only called on macOS)
        runner.RunAsync("brew", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("Homebrew 4.6.10"));

        // node --version
        runner.RunAsync("node", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("v20.11.0"));

        // npm --version
        runner.RunAsync("npm", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("10.2.4"));

        // java -version
        runner.RunAsync("java", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult(stdOut: "", stdErr: "openjdk version \"21.0.3\" 2024-01-16"));

        // adb version — real output has two "version" lines; the second (capital "V") carries the
        // actual platform-tools build number, while the first is a frozen protocol version (1.0.41
        // essentially forever) that must NOT be mistaken for it.
        runner.RunAsync("adb", "version", Arg.Any<CancellationToken>())
              .Returns(OkResult("Android Debug Bridge version 1.0.41\nVersion 35.0.2-12147458\nInstalled as /platform-tools/adb"));

        // xcodebuild -version (only called on macOS)
        runner.RunAsync("xcodebuild", "-version", Arg.Any<CancellationToken>())
              .Returns(OkResult("Xcode 15.2\nBuild version 15C500b"));

        // xcode-select -p (only called on macOS)
        runner.RunAsync("xcode-select", "-p", Arg.Any<CancellationToken>())
              .Returns(OkResult("/Library/Developer/CommandLineTools"));

        // appium --version
        runner.RunAsync("appium", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("2.5.4"));

        // appium driver list --installed --json
        runner.RunAsync("appium", "driver list --installed --json", Arg.Any<CancellationToken>())
              .Returns(OkResult(
                  "{\"uiautomator2\":{\"version\":\"3.10.0\",\"installed\":true}," +
                  "\"xcuitest\":{\"version\":\"7.10.1\",\"installed\":true}}"));
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
        node.InstallPath.Should().Be("/usr/local/bin/node");
    }

    [Fact]
    public async Task ProbeNpm_KnownGoodOutput_ReturnsFoundWithInstallPath()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("npm", "--version", Arg.Any<CancellationToken>())
              .Returns(OkResult("10.2.4"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var npm = results.Single(r => r.Name == "npm");
        npm.State.Should().Be(DetectionState.Found);
        npm.InstallPath.Should().Be("/usr/local/bin/npm");
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
    public async Task ProbeAdb_RealWorldOutput_UsesPlatformToolsVersionNotProtocolVersion()
    {
        // Regression test for a real bug: the frozen "Android Debug Bridge version 1.0.41" protocol
        // line was being parsed as if it were the platform-tools build version, permanently
        // misclassifying ADB as Outdated no matter how current it actually was — making the
        // Dashboard's "Update" button for ADB look like it did nothing, forever.
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("adb", "version", Arg.Any<CancellationToken>())
              .Returns(OkResult("Android Debug Bridge version 1.0.41\nVersion 37.0.0-14910828\nInstalled as /platform-tools/adb"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var adb = results.Single(r => r.Name == "ADB");
        adb.State.Should().Be(DetectionState.Found);
        adb.InstalledVersion.Should().Be("37.0.0");
    }

    [Fact]
    public async Task ProbeAdb_TrulyOldPlatformTools_StillReportsOutdated()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("adb", "version", Arg.Any<CancellationToken>())
              .Returns(OkResult("Android Debug Bridge version 1.0.39\nVersion 28.0.2-5326256\nInstalled as /platform-tools/adb"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        var adb = results.Single(r => r.Name == "ADB");
        adb.State.Should().Be(DetectionState.Outdated);
    }

    [Fact]
    public async Task ProbeDrivers_JsonOutput_SurfacesKnownDriversWithVersion()
    {
        var runner = CreateRunner();
        var platform = MacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("appium", "driver list --installed --json", Arg.Any<CancellationToken>())
              .Returns(OkResult(
                  "{\"uiautomator2\":{\"version\":\"3.10.0\",\"installed\":true}," +
                  "\"xcuitest\":{\"version\":\"7.10.1\",\"installed\":true}}"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        // Known drivers are reported under their InstallCatalog names.
        var ua2 = results.Single(r => r.Name == "UiAutomator2 Driver");
        ua2.State.Should().Be(DetectionState.Found);
        ua2.InstalledVersion.Should().Be("3.10.0");

        var xcui = results.Single(r => r.Name == "XCUITest Driver");
        xcui.State.Should().Be(DetectionState.Found);
        xcui.InstalledVersion.Should().Be("7.10.1");
    }

    [Fact]
    public async Task ProbeDrivers_TextOutput_ParsedAsFallback_WithExtraDriver()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        // Non-JSON (human-readable) output — the text fallback should still extract names/versions.
        runner.RunAsync("appium", "driver list --installed --json", Arg.Any<CancellationToken>())
              .Returns(OkResult("- uiautomator2@3.10.0 [installed (npm)]\n- flutter@2.4.1 [installed (npm)]"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        // uiautomator2 is a known driver (catalog name); flutter surfaces dynamically.
        results.Single(r => r.Name == "UiAutomator2 Driver").InstalledVersion.Should().Be("3.10.0");
        results.Single(r => r.Name == "flutter driver").InstalledVersion.Should().Be("2.4.1");
    }

    [Fact]
    public async Task ProbeDrivers_NoneInstalled_KnownDriversReportedMissing()
    {
        var runner = CreateRunner();
        var platform = MacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("appium", "driver list --installed --json", Arg.Any<CancellationToken>())
              .Returns(OkResult("{}"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        results.Single(r => r.Name == "UiAutomator2 Driver").State.Should().Be(DetectionState.NotFound);
        results.Single(r => r.Name == "XCUITest Driver").State.Should().Be(DetectionState.NotFound);
    }

    [Fact]
    public async Task ProbeDrivers_XcuiTest_NonMacOs_ReturnsNotApplicable()
    {
        var runner = CreateRunner();
        var platform = NonMacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("appium", "driver list --installed --json", Arg.Any<CancellationToken>())
              .Returns(OkResult("{}"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        results.Single(r => r.Name == "XCUITest Driver").State.Should().Be(DetectionState.NotApplicable);
    }

    [Fact]
    public async Task ProbeDrivers_AppiumMissing_KnownDriversReportedMissing()
    {
        var runner = CreateRunner();
        var platform = MacPlatform();
        WireAllInstantMocks(runner, platform);
        runner.RunAsync("appium", "driver list --installed --json", Arg.Any<CancellationToken>())
              .Returns(FailResult("command not found: appium"));

        var sut = new DetectionService(runner, platform);
        var results = await sut.ScanAllAsync();

        // Appium's error output must not be parsed as a driver.
        results.Should().NotContain(r => r.Name == "command driver");
        results.Single(r => r.Name == "UiAutomator2 Driver").State.Should().Be(DetectionState.NotFound);
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

        results.Should().HaveCount(13);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ScanAllAsync_RealCommandRunner_MissingToolDoesNotCrashTheWholeScan()
    {
        // Regression test for a real bug: CommandRunner.RunAsync used to let Process.Start()'s
        // Win32Exception (executable not found) propagate uncaught. Since ScanAllAsync's very first
        // line fetches the Appium driver list with no try/catch, that meant the *entire* scan blew up
        // — not just one probe — on any machine missing any of the tools this service probes for,
        // which is precisely the machine state this app exists to help with. Uses the real
        // CommandRunner (no mocking) so it actually exercises process-start failure, not a mock that
        // can't fail. Deliberately does not assert which specific tools are Found/NotFound — that's
        // real, mutable state on whatever machine runs this suite (e.g. Appium may get installed by
        // someone using the app between test runs); the invariant under test is only that a missing
        // tool never crashes the whole scan.
        var logService = Substitute.For<ILogService>();
        var runner = new CommandRunner(logService);
        var platform = NonMacPlatform();

        var sut = new DetectionService(runner, platform);

        var results = await sut.ScanAllAsync();

        results.Should().HaveCount(13);
    }
}
