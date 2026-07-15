using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Services;

namespace AppiumSetupManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboard;
    private readonly InstallViewModel   _install;
    private readonly DoctorViewModel    _doctor    = new();
    private readonly StorageViewModel   _storage   = new();

    public CommandLogViewModel CommandLog { get; }

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private string _activeNav = "Dashboard";

    public MainWindowViewModel(ILogService logService, IDetectionService detectionService, InstallViewModel installViewModel)
    {
        _dashboard   = new DashboardViewModel(detectionService);
        _install     = installViewModel;
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
