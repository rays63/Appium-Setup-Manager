using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.ViewModels;
using AppiumSetupManager.Views;

namespace AppiumSetupManager;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var platform   = PlatformAdapterFactory.Create();
            var logService = new LogService(platform);
            var runner     = new CommandRunner(logService);
            var envManager = new EnvironmentVariableManager(platform);
            var detection  = new DetectionService(runner, platform);
            var installer   = new InstallerService(runner, platform, envManager, detection);
            var installVm   = new InstallViewModel(installer, detection);
            var doctorService = new DoctorService(runner, platform);
            var doctorVm    = new DoctorViewModel(doctorService);
            var mainVm      = new MainWindowViewModel(logService, detection, installer, platform, envManager, installVm, doctorVm);

            desktop.MainWindow = new MainWindow { DataContext = mainVm };

            desktop.ShutdownRequested += (_, _) =>
            {
                mainVm.CommandLog.Shutdown();
                installVm.Dispose();
                doctorVm.Dispose();
                logService.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
