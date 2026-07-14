using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            var mainVm     = new MainWindowViewModel(logService);

            desktop.MainWindow = new MainWindow { DataContext = mainVm };

            desktop.ShutdownRequested += (_, _) =>
            {
                mainVm.CommandLog.Shutdown();
                logService.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
