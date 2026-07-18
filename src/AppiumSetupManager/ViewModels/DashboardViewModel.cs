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

    // ── Environment health ───────────────────────────────────────────────────

    [ObservableProperty]
    private double _environmentHealthPercent;

    [ObservableProperty]
    private string _environmentHealthSubtext = string.Empty;

    // ── Hero "Quick Install" CTA ──────────────────────────────────────────────

    [ObservableProperty]
    private bool _isQuickInstalling;

    private CancellationTokenSource _quickInstallCts = new();

    /// <summary>
    /// Raised when the user clicks the hero's "Advanced Install" CTA. DashboardViewModel has no
    /// navigation concept of its own (that lives in MainWindowViewModel, which owns the nav state
    /// for every screen) — MainWindowViewModel subscribes to this and switches CurrentView to the
    /// Install screen, the same indirection pattern InstallViewModel.InstallCompleted already uses.
    /// </summary>
    public event Action? AdvancedInstallRequested;

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
    private Task RescanAsync() => ScanAsync();

    private async Task ScanAsync()
    {
        _scanCts.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        IsScanning = true;
        RescanCommand.NotifyCanExecuteChanged();
        Components.Clear();
        PathValue = Environment.GetEnvironmentVariable("PATH");

        try
        {
            var results = await _detection.ScanAllAsync(ct);

            foreach (var result in results)
                Components.Add(new ComponentStatusViewModel(result, InstallComponentAsync));

            JavaHomeItem    = Components.FirstOrDefault(c => c.Name == "JAVA_HOME");
            AndroidHomeItem = Components.FirstOrDefault(c => c.Name == "ANDROID_HOME");
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
    /// Hero "Quick Install" CTA — installs every missing/outdated component in one pass via the same
    /// IInstallerService already used by the per-tile Install buttons (InstallComponentAsync above).
    /// Progress streams to the command log automatically (ICommandRunner logs through ILogService
    /// regardless of caller), so there is nothing extra to render here beyond a busy spinner.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanQuickInstall))]
    private async Task QuickInstallAsync()
    {
        _quickInstallCts.Cancel();
        _quickInstallCts = new CancellationTokenSource();
        var ct = _quickInstallCts.Token;

        IsQuickInstalling = true;
        QuickInstallCommand.NotifyCanExecuteChanged();
        RescanCommand.NotifyCanExecuteChanged();

        try
        {
            await foreach (var _ in _installer.InstallAllAsync(ct))
            {
                // Steps are surfaced through the command log; nothing to render on the dashboard.
            }
        }
        catch (OperationCanceledException)
        {
            // Non-fatal — fall through to a re-scan so the dashboard reflects whatever state resulted.
        }
        finally
        {
            IsQuickInstalling = false;
            QuickInstallCommand.NotifyCanExecuteChanged();
        }

        await RescanAsync();
    }

    private bool CanQuickInstall() => !IsQuickInstalling && !IsScanning;

    [RelayCommand]
    private void AdvancedInstall() => AdvancedInstallRequested?.Invoke();

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
        _quickInstallCts.Cancel();
        _quickInstallCts.Dispose();
    }
}
