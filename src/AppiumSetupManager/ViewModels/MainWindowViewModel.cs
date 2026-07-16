using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;

namespace AppiumSetupManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboard;
    private readonly InstallViewModel   _install;
    private readonly DoctorViewModel    _doctor;
    private readonly StorageViewModel   _storage   = new();

    public CommandLogViewModel CommandLog { get; }

    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private string _activeNav = "Dashboard";

    public MainWindowViewModel(ILogService logService, IDetectionService detectionService, IInstallerService installerService, IPlatformAdapter platform, IEnvironmentVariableManager envManager, InstallViewModel installViewModel, DoctorViewModel doctorViewModel)
    {
        _dashboard   = new DashboardViewModel(detectionService, installerService, platform, envManager);
        _install     = installViewModel;
        _doctor      = doctorViewModel;
        CommandLog   = new CommandLogViewModel(logService);
        _currentView = _dashboard;

        installViewModel.InstallCompleted += () =>
            Dispatcher.UIThread.Post(() =>
            {
                CurrentView = _doctor;
                ActiveNav   = "Doctor";
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
}
