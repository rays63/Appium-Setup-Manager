using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IDetectionService _detection;
    private readonly IInstallerService _installer;
    private readonly IEnvironmentVariableManager _envManager;
    private CancellationTokenSource _scanCts = new();

    public ObservableCollection<ComponentStatusViewModel> Components { get; } = new();

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _summaryText = Strings.DashboardScanning;

    // ── Platform info card ───────────────────────────────────────────────────

    public string PlatformName => _platform.OsDisplayName;
    public string PlatformArchitecture => RuntimeInformation.OSArchitecture.ToString();
    public string PlatformKernel => RuntimeInformation.OSDescription;
    public bool IsMacOs => _platform.IsMacOs;

    [ObservableProperty]
    private string? _xcodeVersionText;

    // ── Global environment variables ─────────────────────────────────────────

    [ObservableProperty]
    private string? _pathValue;

    [ObservableProperty]
    private ComponentStatusViewModel? _javaHomeItem;

    [ObservableProperty]
    private ComponentStatusViewModel? _androidHomeItem;

    [ObservableProperty]
    private bool _isConfiguringJavaHome;

    [ObservableProperty]
    private bool _isConfiguringAndroidHome;

    [ObservableProperty]
    private string? _javaHomeConfigureMessage;

    [ObservableProperty]
    private string? _androidHomeConfigureMessage;

    // ── System paths ─────────────────────────────────────────────────────────

    [ObservableProperty]
    private ComponentStatusViewModel? _appiumItem;

    [ObservableProperty]
    private ComponentStatusViewModel? _adbItem;

    [ObservableProperty]
    private ComponentStatusViewModel? _nodeItem;

    // ── Environment health ───────────────────────────────────────────────────

    [ObservableProperty]
    private double _environmentHealthPercent;

    [ObservableProperty]
    private string _environmentHealthSubtext = string.Empty;

    private readonly IPlatformAdapter _platform;

    public DashboardViewModel(IDetectionService detectionService, IInstallerService installerService, IPlatformAdapter platform, IEnvironmentVariableManager envManager)
    {
        _detection  = detectionService;
        _installer  = installerService;
        _platform   = platform;
        _envManager = envManager;
        _ = RescanAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRescan))]
    private Task RescanAsync() => ScanAsync(clearConfigureMessages: true);

    /// <summary>
    /// Runs the actual scan. <paramref name="clearConfigureMessages"/> is false when called right
    /// after ConfigureJavaHomeAsync/ConfigureAndroidHomeAsync, so the not-found message they just set
    /// survives the follow-up re-scan instead of being wiped before the user ever sees it.
    /// </summary>
    private async Task ScanAsync(bool clearConfigureMessages)
    {
        _scanCts.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;
        RescanCommand.NotifyCanExecuteChanged();
        Components.Clear();
        PathValue = Environment.GetEnvironmentVariable("PATH");
        if (clearConfigureMessages)
        {
            JavaHomeConfigureMessage = null;
            AndroidHomeConfigureMessage = null;
        }

        try
        {
            var results = await _detection.ScanAllAsync(ct);

            foreach (var result in results)
                Components.Add(new ComponentStatusViewModel(result, InstallComponentAsync));

            JavaHomeItem    = Components.FirstOrDefault(c => c.Name == "JAVA_HOME");
            AndroidHomeItem = Components.FirstOrDefault(c => c.Name == "ANDROID_HOME");
            AppiumItem       = Components.FirstOrDefault(c => c.Name == "Appium");
            AdbItem          = Components.FirstOrDefault(c => c.Name == "ADB");
            NodeItem         = Components.FirstOrDefault(c => c.Name == "Node.js");
            XcodeVersionText = Components.FirstOrDefault(c => c.Name == "Xcode")?.VersionDisplay;

            var applicable = results.Where(r => r.State != DetectionState.NotApplicable).ToList();
            var found      = applicable.Count(r => r.State == DetectionState.Found);
            SummaryText     = string.Format(Strings.DashboardSummaryFormat, found, applicable.Count);

            var issues = applicable.Count - found;
            EnvironmentHealthPercent = applicable.Count > 0 ? 100.0 * found / applicable.Count : 0;
            EnvironmentHealthSubtext = issues == 0
                ? Strings.DashboardHealthAllClear
                : string.Format(Strings.DashboardHealthIssuesFormat, issues);
        }
        catch (OperationCanceledException)
        {
            SummaryText = Strings.DashboardScanCancelled;
        }
        catch (Exception)
        {
            SummaryText = Strings.DashboardScanFailed;
        }
        finally
        {
            IsScanning = false;
            RescanCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRescan() => !IsScanning;

    /// <summary>
    /// Looks for a JDK already installed outside this app (e.g. bundled with Android Studio) and, if
    /// found, points JAVA_HOME at it — no package install required.
    /// </summary>
    [RelayCommand]
    private async Task ConfigureJavaHomeAsync()
    {
        IsConfiguringJavaHome = true;
        JavaHomeConfigureMessage = null;
        try
        {
            var resolved = await _envManager.TryConfigureJavaHomeAsync();
            if (resolved is null)
                JavaHomeConfigureMessage = Strings.DashboardNoExistingInstallFound;
        }
        finally
        {
            IsConfiguringJavaHome = false;
        }

        await ScanAsync(clearConfigureMessages: false);
    }

    /// <summary>
    /// Looks for an Android SDK already installed at the platform default location (e.g. via
    /// Android Studio) and, if found, points ANDROID_HOME at it — no package install required.
    /// </summary>
    [RelayCommand]
    private async Task ConfigureAndroidHomeAsync()
    {
        IsConfiguringAndroidHome = true;
        AndroidHomeConfigureMessage = null;
        try
        {
            var resolved = await _envManager.TryConfigureAndroidHomeAsync();
            if (resolved is null)
                AndroidHomeConfigureMessage = Strings.DashboardNoExistingInstallFound;
        }
        finally
        {
            IsConfiguringAndroidHome = false;
        }

        await ScanAsync(clearConfigureMessages: false);
    }

    /// <summary>
    /// Installs a single component via the shared installer, then re-scans so the dashboard reflects
    /// the new state. Progress is streamed to the command log by the installer/command runner.
    /// </summary>
    private async Task InstallComponentAsync(ComponentStatusViewModel component)
    {
        if (component.CatalogName is null)
            return;

        var onlySkipped = true;
        try
        {
            await foreach (var step in _installer.InstallSelectedAsync(new[] { component.CatalogName }))
            {
                // Steps are surfaced through the command log; nothing to render on the dashboard.
                if (step.State != InstallStepState.Skipped)
                    onlySkipped = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is non-fatal — fall through to a re-scan to show whatever state resulted.
            onlySkipped = false;
        }

        // The underlying package (e.g. a JDK) may already be installed, so the install step above
        // was rightfully Skipped — but that means it never ran the post-install env var setup either.
        // For JAVA_HOME/ANDROID_HOME specifically, fall back to pointing at the existing install so
        // clicking "Install" here still resolves the actual thing the user is missing.
        if (onlySkipped)
        {
            if (component.Name == "JAVA_HOME")
                await _envManager.TryConfigureJavaHomeAsync();
            else if (component.Name == "ANDROID_HOME")
                await _envManager.TryConfigureAndroidHomeAsync();
        }

        await RescanAsync();
    }

    public void Dispose()
    {
        _scanCts.Cancel();
        _scanCts.Dispose();
    }
}
