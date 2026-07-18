using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AppiumSetupManager.Tests.Services;

public class InstallerServiceTests
{
    // ── Factories ────────────────────────────────────────────────────────────

    private static IPlatformAdapter MacPlatform()
    {
        var p = Substitute.For<IPlatformAdapter>();
        p.IsMacOs.Returns(true);
        p.IsWindows.Returns(false);
        p.IsLinux.Returns(false);
        p.DefaultAndroidSdkPath.Returns("/Users/test/Android/Sdk");
        return p;
    }

    private static IPlatformAdapter WindowsPlatform()
    {
        var p = Substitute.For<IPlatformAdapter>();
        p.IsMacOs.Returns(false);
        p.IsWindows.Returns(true);
        p.IsLinux.Returns(false);
        p.DefaultAndroidSdkPath.Returns(@"C:\Users\test\AppData\Local\Android\Sdk");
        return p;
    }

    private static ICommandRunner DefaultRunner()
    {
        var r = Substitute.For<ICommandRunner>();
        r.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
         .Returns(new CommandResult("cmd", 0, "", "", false));
        return r;
    }

    private static IEnvironmentVariableManager DefaultEnvManager() =>
        Substitute.For<IEnvironmentVariableManager>();

    /// <summary>
    /// Builds a mock IDetectionService whose ScanAllAsync returns ComponentStatus records
    /// keyed by catalog component names. Any name not in <paramref name="overrides"/>
    /// defaults to DetectionState.Found.
    /// </summary>
    private static IDetectionService MockDetection(params (string name, DetectionState state)[] overrides)
    {
        var d = Substitute.For<IDetectionService>();
        var all = InstallCatalog.All.Select(e =>
        {
            var o = overrides.FirstOrDefault(x => x.name == e.ComponentName);
            var state = o != default ? o.state : DetectionState.Found;
            return new ComponentStatus(e.ComponentName, state, null, null, null, null);
        }).ToList();
        d.ScanAllAsync(Arg.Any<CancellationToken>())
         .Returns(Task.FromResult<IReadOnlyList<ComponentStatus>>(all));
        return d;
    }

    private static IHistoryService DefaultHistory() =>
        Substitute.For<IHistoryService>();

    private static InstallerService CreateService(
        ICommandRunner? runner = null,
        IPlatformAdapter? platform = null,
        IEnvironmentVariableManager? envManager = null,
        IDetectionService? detection = null,
        IHistoryService? history = null)
    {
        return new InstallerService(
            runner    ?? DefaultRunner(),
            platform  ?? MacPlatform(),
            envManager ?? DefaultEnvManager(),
            detection ?? MockDetection(),
            history   ?? DefaultHistory());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<List<InstallStep>> CollectAsync(
        IAsyncEnumerable<InstallStep> steps,
        CancellationToken ct = default)
    {
        var list = new List<InstallStep>();
        await foreach (var s in steps.WithCancellation(ct))
            list.Add(s);
        return list;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InstallAll_FoundComponent_EmitsSkipped()
    {
        var runner = DefaultRunner();
        var detection = MockDetection(("Node.js", DetectionState.Found));

        var sut = CreateService(runner: runner, detection: detection);
        var steps = await CollectAsync(sut.InstallAllAsync());

        var nodeSteps = steps.Where(s => s.ComponentName == "Node.js").ToList();
        nodeSteps.Should().ContainSingle()
                 .Which.State.Should().Be(InstallStepState.Skipped);

        // No RunAsync call for brew/node since it was skipped.
        await runner.DidNotReceive().RunAsync("brew", Arg.Is<string>(a => a.Contains("node")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAll_OutdatedComponent_RunsInstall()
    {
        var runner = DefaultRunner();
        // All other components are Found; Node.js is Outdated so it must be installed.
        var detection = MockDetection(("Node.js", DetectionState.Outdated));

        var sut = CreateService(runner: runner, detection: detection);
        await CollectAsync(sut.InstallAllAsync());

        await runner.Received(1).RunAsync("brew", "install node", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAll_ThreeStateProtocol()
    {
        // All Found except Appium; only Appium steps should be Pending → Running → Done.
        var detection = MockDetection(("Appium", DetectionState.NotFound));

        var sut = CreateService(detection: detection);
        var steps = await CollectAsync(sut.InstallAllAsync());

        var appiumSteps = steps.Where(s => s.ComponentName == "Appium").ToList();
        appiumSteps.Should().HaveCount(3);
        appiumSteps[0].State.Should().Be(InstallStepState.Pending);
        appiumSteps[1].State.Should().Be(InstallStepState.Running);
        appiumSteps[2].State.Should().Be(InstallStepState.Done);
    }

    [Fact]
    public async Task InstallAll_StepOrdering()
    {
        // All Found except Appium and UiAutomator2 Driver.
        var detection = MockDetection(
            ("Appium", DetectionState.NotFound),
            ("UiAutomator2 Driver", DetectionState.NotFound));

        var sut = CreateService(detection: detection);
        var steps = await CollectAsync(sut.InstallAllAsync());

        // Collect the last (terminal) step per component to determine their relative order.
        var terminalSteps = steps
            .GroupBy(s => s.ComponentName)
            .Select(g => (Name: g.Key, FirstIndex: steps.IndexOf(g.First())))
            .ToList();

        var appiumIdx = terminalSteps.First(x => x.Name == "Appium").FirstIndex;
        var uia2Idx   = terminalSteps.First(x => x.Name == "UiAutomator2 Driver").FirstIndex;

        appiumIdx.Should().BeLessThan(uia2Idx);
    }

    [Fact]
    public async Task InstallAll_RealisticScan_SkipsJdk21AndAndroidSdkWhenAlreadyPresent()
    {
        // Regression test for a real bug: DetectionService reports "JDK" and "ADB" (never "JDK 21"
        // or "Android SDK" — those are just the catalog's user-facing names), so GetSkipSet's plain
        // name match could never recognize either as already installed. Every "Install All Missing"
        // run — and every click of ADB's/JAVA_HOME's "Update"/"Install" button — re-ran the full
        // brew/winget/apt install command even when nothing needed installing. MockDetection's
        // helper fabricates catalog-shaped names and would never have caught this, so this test
        // builds a scan shaped like the real DetectionService output instead.
        var runner = DefaultRunner();
        var detection = Substitute.For<IDetectionService>();
        IReadOnlyList<ComponentStatus> realisticScan = InstallCatalog.All
            .Where(e => e.ComponentName is not "JDK 21" and not "Android SDK")
            .Select(e => new ComponentStatus(e.ComponentName, DetectionState.Found, null, null, null, null))
            .Concat(new[]
            {
                new ComponentStatus("JDK", DetectionState.Found, "openjdk 25", null, null, null),
                new ComponentStatus("ADB", DetectionState.Found, "37.0.0", null, null, null),
            })
            .ToList();
        detection.ScanAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(realisticScan));

        var sut = CreateService(runner: runner, detection: detection);
        var steps = await CollectAsync(sut.InstallAllAsync());

        steps.Where(s => s.ComponentName == "JDK 21").Should().ContainSingle()
             .Which.State.Should().Be(InstallStepState.Skipped);
        steps.Where(s => s.ComponentName == "Android SDK").Should().ContainSingle()
             .Which.State.Should().Be(InstallStepState.Skipped);

        await runner.DidNotReceive().RunAsync("brew", Arg.Is<string>(a => a.Contains("temurin")), Arg.Any<CancellationToken>());
        await runner.DidNotReceive().RunAsync("brew", Arg.Is<string>(a => a.Contains("android-commandlinetools")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAll_AndroidHome_SetAfterSuccess()
    {
        var runner = DefaultRunner();
        var envManager = DefaultEnvManager();
        var detection = MockDetection(("Android SDK", DetectionState.NotFound));

        var sut = CreateService(runner: runner, envManager: envManager, detection: detection);
        await CollectAsync(sut.InstallAllAsync());

        await envManager.Received(1).SetUserAsync(
            "ANDROID_HOME",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAll_JdkInstall_TriesToConfigureJavaHomeAfterSuccess()
    {
        var runner = DefaultRunner();
        var envManager = DefaultEnvManager();
        var detection = MockDetection(("JDK 21", DetectionState.NotFound));

        var sut = CreateService(runner: runner, envManager: envManager, detection: detection);
        await CollectAsync(sut.InstallAllAsync());

        await envManager.Received(1).TryConfigureJavaHomeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstallAll_FailureContinues()
    {
        var runner = DefaultRunner();
        // Make Appium installation fail.
        runner.RunAsync("npm", "install -g appium", Arg.Any<CancellationToken>())
              .Returns(new CommandResult("npm install -g appium", 1, "", "npm error: permission denied", false));

        var detection = MockDetection(
            ("Appium", DetectionState.NotFound),
            ("UiAutomator2 Driver", DetectionState.NotFound));

        var sut = CreateService(runner: runner, detection: detection);
        var steps = await CollectAsync(sut.InstallAllAsync());

        steps.Should().Contain(s => s.ComponentName == "Appium" && s.State == InstallStepState.Failed);

        // UiAutomator2 Driver should also appear — at minimum a Pending step.
        steps.Should().Contain(s => s.ComponentName == "UiAutomator2 Driver");
    }

    [Fact]
    public async Task InstallAll_Cancellation_StopsIteration()
    {
        using var cts = new CancellationTokenSource();

        var runner = DefaultRunner();
        int runAsyncCallCount = 0;

        // Cancel after first Running step is emitted.
        // We achieve this by cancelling inside the RunAsync mock after the first call.
        runner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(callInfo =>
              {
                  runAsyncCallCount++;
                  cts.Cancel();
                  return Task.FromResult(new CommandResult("cmd", 0, "", "", false));
              });

        // All components NotFound so every step triggers RunAsync.
        var detection = MockDetection(
            ("Node.js",            DetectionState.NotFound),
            ("npm",                DetectionState.NotFound),
            ("JDK 21",             DetectionState.NotFound),
            ("Android SDK",        DetectionState.NotFound),
            ("Appium",             DetectionState.NotFound),
            ("UiAutomator2 Driver",DetectionState.NotFound),
            ("Xcode CLI Tools",    DetectionState.NotFound),
            ("XCUITest Driver",    DetectionState.NotFound),
            ("Appium Inspector",   DetectionState.NotFound));

        var sut = CreateService(runner: runner, detection: detection);

        var steps = new List<InstallStep>();
        try
        {
            await foreach (var s in sut.InstallAllAsync(cts.Token))
                steps.Add(s);
        }
        catch (OperationCanceledException) { /* expected */ }

        // RunAsync should have been called at most once — only for the first non-skipped component.
        runAsyncCallCount.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task InstallAll_MacOnlySkippedOnWindows()
    {
        var platform = WindowsPlatform();
        var detection = MockDetection(); // all Found — we only care about which names appear

        var sut = CreateService(platform: platform, detection: detection);
        var steps = await CollectAsync(sut.InstallAllAsync());

        var names = steps.Select(s => s.ComponentName).Distinct().ToList();
        names.Should().NotContain("Xcode CLI Tools");
        names.Should().NotContain("XCUITest Driver");
    }

    [Fact]
    public async Task InstallAll_Timeout_EmitsFailed()
    {
        var runner = DefaultRunner();
        runner.RunAsync("npm", "install -g appium", Arg.Any<CancellationToken>())
              .Returns(new CommandResult("npm install -g appium", -1, "", "Timed out.", TimedOut: true));

        var detection = MockDetection(("Appium", DetectionState.NotFound));

        var sut = CreateService(runner: runner, detection: detection);
        var steps = await CollectAsync(sut.InstallAllAsync());

        var failedStep = steps.Single(s => s.ComponentName == "Appium" && s.State == InstallStepState.Failed);
        failedStep.ErrorMessage.Should().NotBeNull();
        failedStep.ErrorMessage!.ToLowerInvariant().Should().Contain("timed out");
    }

    [Fact]
    public async Task InstallSelected_FiltersToNamedComponent()
    {
        var detection = MockDetection(("Appium", DetectionState.NotFound));

        var sut = CreateService(detection: detection);
        var steps = await CollectAsync(sut.InstallSelectedAsync(new[] { "Appium" }));

        var names = steps.Select(s => s.ComponentName).Distinct().ToList();
        names.Should().ContainSingle().Which.Should().Be("Appium");
    }

    // ── UpdateSelectedAsync vs InstallSelectedAsync skip-logic contrast ────────
    //
    // These two tests use the exact same "Appium is Found" scan to make the behavioral difference
    // between the two methods explicit: InstallSelectedAsync is correct to skip a Found component
    // (the Install screen has nothing to do once something's already present), but
    // UpdateSelectedAsync must NEVER skip a Found component — the whole point of the Updates screen
    // is to re-check/re-run an install command for something that's already installed but may be
    // outdated per a live npm version check the ViewModel performed. Before this fix, both methods
    // shared the same skip set and "Update" on an already-Found-but-npm-outdated component (e.g.
    // Appium) silently no-op'ed via a Skipped step.

    [Fact]
    public async Task InstallSelected_FoundComponent_EmitsSkipped()
    {
        var runner = DefaultRunner();
        var detection = MockDetection(("Appium", DetectionState.Found));

        var sut = CreateService(runner: runner, detection: detection);
        var steps = await CollectAsync(sut.InstallSelectedAsync(new[] { "Appium" }));

        steps.Should().ContainSingle().Which.State.Should().Be(InstallStepState.Skipped);
        await runner.DidNotReceive().RunAsync("npm", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSelected_FoundComponent_DoesNotSkip_RunsInstallCommand()
    {
        var runner = DefaultRunner();
        var detection = MockDetection(("Appium", DetectionState.Found));

        var sut = CreateService(runner: runner, detection: detection);
        var steps = await CollectAsync(sut.UpdateSelectedAsync(new[] { "Appium" }));

        steps.Should().NotContain(s => s.State == InstallStepState.Skipped);
        steps.Should().Contain(s => s.ComponentName == "Appium" && s.State == InstallStepState.Done);
        await runner.Received(1).RunAsync("npm", "install -g appium", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSelected_NotApplicableComponent_StillSkipped()
    {
        // NotApplicable (e.g. an iOS-only component on a non-mac platform) must still be skipped by
        // UpdateSelectedAsync — only Found is exempted from the skip set, per the fix above.
        var platform = WindowsPlatform();
        var detection = MockDetection(); // all Found by default; XCUITest Driver is MacOnly so its
                                          // command resolves to null on Windows regardless of state.

        var sut = CreateService(platform: platform, detection: detection);
        var steps = await CollectAsync(sut.UpdateSelectedAsync(new[] { "XCUITest Driver" }));

        // Null command on this platform means the entry is silently omitted — no steps at all.
        steps.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateSelected_FiltersToNamedComponent()
    {
        var detection = MockDetection(("Appium", DetectionState.Found));

        var sut = CreateService(detection: detection);
        var steps = await CollectAsync(sut.UpdateSelectedAsync(new[] { "Appium" }));

        var names = steps.Select(s => s.ComponentName).Distinct().ToList();
        names.Should().ContainSingle().Which.Should().Be("Appium");
    }
}
