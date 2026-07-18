using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Services;

namespace AppiumSetupManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    private readonly DashboardViewModel   _dashboard;
    private readonly InstallViewModel     _install;
    private readonly DoctorViewModel      _doctor;
    private readonly StorageViewModel     _storage;
    private readonly EnvironmentViewModel _environment;
    private readonly UpdatesViewModel     _updates;
    private readonly HistoryViewModel     _history;
    private readonly LogsViewModel        _logs;
    private readonly SettingsViewModel    _settings;

    public CommandLogViewModel CommandLog { get; }

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private string _activeNav = "Dashboard";

    [ObservableProperty]
    private ThemeMode _activeTheme;

    public MainWindowViewModel(ILogBuffer logBuffer, IDetectionService detectionService, IInstallerService installerService, IPlatformAdapter platform, IEnvironmentVariableManager envManager, IThemeService themeService, InstallViewModel installViewModel, DoctorViewModel doctorViewModel, StorageViewModel storageViewModel, EnvironmentViewModel environmentViewModel, UpdatesViewModel updatesViewModel, HistoryViewModel historyViewModel, LogsViewModel logsViewModel, SettingsViewModel settingsViewModel)
    {
        _themeService = themeService;
        _dashboard    = new DashboardViewModel(detectionService, installerService, platform, envManager);
        _install      = installViewModel;
        _doctor       = doctorViewModel;
        _storage      = storageViewModel;
        _environment  = environmentViewModel;
        _updates      = updatesViewModel;
        _history      = historyViewModel;
        _logs         = logsViewModel;
        _settings     = settingsViewModel;
        CommandLog    = new CommandLogViewModel(logBuffer);
        _currentView  = _dashboard;
        _activeTheme  = _themeService.CurrentMode;

        installViewModel.InstallCompleted += () =>
            Dispatcher.UIThread.Post(() =>
            {
                CurrentView = _doctor;
                ActiveNav   = "Doctor";
            });

        // Dashboard's hero "Advanced Install" CTA has no navigation concept of its own — navigation
        // state lives here, same indirection InstallViewModel.InstallCompleted already uses above.
        _dashboard.AdvancedInstallRequested += () =>
            Dispatcher.UIThread.Post(() =>
            {
                CurrentView = _install;
                ActiveNav   = "Install";
            });
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        CurrentView = _dashboard;
        ActiveNav   = "Dashboard";
    }

    [RelayCommand]
    private void NavigateToInstall()
    {
        CurrentView = _install;
        ActiveNav   = "Install";
    }

    [RelayCommand]
    private void NavigateToDoctor()
    {
        CurrentView = _doctor;
        ActiveNav   = "Doctor";
    }

    [RelayCommand]
    private void NavigateToStorage()
    {
        CurrentView = _storage;
        ActiveNav   = "Storage";
    }

    [RelayCommand]
    private void NavigateToEnvironment()
    {
        CurrentView = _environment;
        ActiveNav   = "Environment";
    }

    [RelayCommand]
    private void NavigateToUpdates()
    {
        CurrentView = _updates;
        ActiveNav   = "Updates";
    }

    [RelayCommand]
    private void NavigateToHistory()
    {
        CurrentView = _history;
        ActiveNav   = "History";
    }

    [RelayCommand]
    private void NavigateToLogs()
    {
        CurrentView = _logs;
        ActiveNav   = "Logs";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = _settings;
        ActiveNav   = "Settings";
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var next = ActiveTheme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        _themeService.SetMode(next);
        ActiveTheme = _themeService.CurrentMode;
    }

    // ── Keyboard shortcuts (Settings screen lists these; gestures bound in MainWindow) ─────────
    // Navigate first so the user sees where the action is happening, then trigger the target
    // screen's own command through its public generated command property — never its private
    // method — so CanExecute guards (busy states) are honored.

    /// <summary>Cmd/Ctrl+Shift+I — jump to Dashboard and run its Quick Install.</summary>
    [RelayCommand]
    private void QuickInstallShortcut()
    {
        NavigateToDashboard();
        if (_dashboard.QuickInstallCommand.CanExecute(null))
            _dashboard.QuickInstallCommand.Execute(null);
    }

    /// <summary>Cmd/Ctrl+Shift+D — jump to Doctor and re-run its checks.</summary>
    [RelayCommand]
    private void RunDoctorShortcut()
    {
        NavigateToDoctor();
        if (_doctor.RunChecksCommand.CanExecute(null))
            _doctor.RunChecksCommand.Execute(null);
    }
}
