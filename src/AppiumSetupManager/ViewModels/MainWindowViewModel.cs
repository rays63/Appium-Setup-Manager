using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;
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

    // ── Top-bar quick search (R2 step 3) ─────────────────────────────────────

    /// <summary>Live text of the top-bar search box; results rebuild on every change.</summary>
    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>Whether the results popup under the search box is showing.</summary>
    [ObservableProperty]
    private bool _isSearchOpen;

    public ObservableCollection<SearchResultViewModel> SearchResults { get; } = new();

    private const int MaxSearchResults = 8;

    public MainWindowViewModel(ILogBuffer logBuffer, ILogService logService, IDetectionService detectionService, IInstallerService installerService, IPlatformAdapter platform, IEnvironmentVariableManager envManager, IThemeService themeService, InstallViewModel installViewModel, DoctorViewModel doctorViewModel, StorageViewModel storageViewModel, EnvironmentViewModel environmentViewModel, UpdatesViewModel updatesViewModel, HistoryViewModel historyViewModel, LogsViewModel logsViewModel, SettingsViewModel settingsViewModel)
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
        CommandLog    = new CommandLogViewModel(logBuffer, logService);
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

    // ── Top-bar quick search (R2 step 3) ─────────────────────────────────────
    // Result order: matching screens, then matching dashboard components, then a "search the
    // logs for this" action which is always present for a non-empty query — so the popup is
    // never empty while typing, and Enter always has something sensible to run.

    /// <summary>Nav destinations searchable by their localized sidebar names.</summary>
    private IEnumerable<(string Name, Action Navigate)> ScreenSearchEntries()
    {
        yield return (Strings.NavDashboard,   NavigateToDashboard);
        yield return (Strings.NavInstall,     NavigateToInstall);
        yield return (Strings.NavDoctor,      NavigateToDoctor);
        yield return (Strings.NavEnvironment, NavigateToEnvironment);
        yield return (Strings.NavStorage,     NavigateToStorage);
        yield return (Strings.NavUpdates,     NavigateToUpdates);
        yield return (Strings.NavHistory,     NavigateToHistory);
        yield return (Strings.NavLogs,        NavigateToLogs);
        yield return (Strings.NavSettings,    NavigateToSettings);
    }

    partial void OnSearchQueryChanged(string value) => RebuildSearchResults();

    private void RebuildSearchResults()
    {
        SearchResults.Clear();

        var query = SearchQuery.Trim();
        if (query.Length == 0)
        {
            IsSearchOpen = false;
            return;
        }

        // Screens — reserve the last slot for the always-present logs action.
        foreach (var (name, navigate) in ScreenSearchEntries())
        {
            if (SearchResults.Count >= MaxSearchResults - 1)
                break;

            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                SearchResults.Add(new SearchResultViewModel(
                    name,
                    Strings.SearchGoToScreen,
                    () => { navigate(); ClearSearch(); }));
        }

        // Components from the dashboard's live detection list.
        foreach (var component in _dashboard.Components)
        {
            if (SearchResults.Count >= MaxSearchResults - 1)
                break;

            if (component.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                SearchResults.Add(new SearchResultViewModel(
                    component.Name,
                    component.VersionDisplay ?? component.StatusLabel,
                    () => { NavigateToDashboard(); ClearSearch(); }));
        }

        // Logs action — always last, always present for a non-empty query.
        SearchResults.Add(new SearchResultViewModel(
            string.Format(Strings.SearchLogsForFormat, query),
            null,
            () =>
            {
                _logs.SearchText = query;
                NavigateToLogs();
                ClearSearch();
            }));

        // Defensive only: the logs action above means a non-empty query always has ≥1 result,
        // but if result construction ever changes, an empty popup must show an inert row
        // rather than an empty floating card.
        if (SearchResults.Count == 0)
            SearchResults.Add(new SearchResultViewModel(Strings.SearchNoResults, null, null));

        IsSearchOpen = true;
    }

    /// <summary>Enter in the search box — run the first actionable result.</summary>
    [RelayCommand]
    private void ExecuteFirstSearchResult()
    {
        var first = SearchResults.FirstOrDefault(r => r.IsExecutable);
        first?.ExecuteCommand.Execute(null);
    }

    /// <summary>Escape — clear the query, which also closes the popup.</summary>
    [RelayCommand]
    private void DismissSearch() => ClearSearch();

    /// <summary>
    /// Close the popup without clearing the query (search box lost focus); typing again or
    /// refocusing with a pending query reopens it.
    /// </summary>
    public void CloseSearchPopup() => IsSearchOpen = false;

    /// <summary>Reopen the popup when the box regains focus and still holds a query.</summary>
    public void ReopenSearchIfPending()
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery) && SearchResults.Count > 0)
            IsSearchOpen = true;
    }

    private void ClearSearch()
    {
        // Setting the query to empty rebuilds (clearing results) and closes the popup.
        SearchQuery = string.Empty;
    }
}
