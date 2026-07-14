using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Services;

namespace AppiumSetupManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboard = new();
    private readonly InstallViewModel   _install   = new();
    private readonly DoctorViewModel    _doctor    = new();
    private readonly StorageViewModel   _storage   = new();

    public CommandLogViewModel CommandLog { get; }

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private string _activeNav = "Dashboard";

    public MainWindowViewModel(ILogService logService)
    {
        CommandLog   = new CommandLogViewModel(logService);
        _currentView = _dashboard;
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
}
