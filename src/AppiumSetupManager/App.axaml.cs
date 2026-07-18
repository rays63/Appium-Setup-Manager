using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Services;
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
            // LogBuffer is the ONE reader of logService's single-consumer channel — every
            // log-displaying ViewModel must consume the buffer, never the channel directly.
            var logBuffer  = new LogBuffer(logService);
            var runner     = new CommandRunner(logService);
            var envManager = new EnvironmentVariableManager(platform);
            var detection  = new DetectionService(runner, platform);
            var backupStore = new EnvironmentBackupStore(platform);
            var historyStore = new HistoryStore(platform);
            var settingsStore = new SettingsStore(platform);
            var installer   = new InstallerService(runner, platform, envManager, detection, historyStore);
            var installVm   = new InstallViewModel(installer, detection);
            var doctorService = new DoctorService(runner, platform, historyStore);
            var doctorVm    = new DoctorViewModel(doctorService);
            var storageService = new StorageService(platform);
            var cleanupService = new CleanupService(runner, historyStore, platform);
            var storageVm   = new StorageViewModel(storageService, cleanupService);
            var environmentVm = new EnvironmentViewModel(detection, envManager, backupStore, historyStore);
            var updateCheck = new UpdateCheckService();
            var updatesVm   = new UpdatesViewModel(detection, installer, updateCheck, settingsStore);
            var historyVm   = new HistoryViewModel(historyStore, backupStore, envManager);
            var logsVm      = new LogsViewModel(logBuffer);
            var settingsVm  = new SettingsViewModel(settingsStore, platform);
            var themeStore  = new SettingsThemePreferenceStore(settingsStore);
            var themeService = new ThemeService(themeStore);
            var mainVm      = new MainWindowViewModel(logBuffer, logService, detection, installer, platform, envManager, themeService, installVm, doctorVm, storageVm, environmentVm, updatesVm, historyVm, logsVm, settingsVm);

            desktop.MainWindow = new MainWindow { DataContext = mainVm };

            desktop.ShutdownRequested += (_, _) =>
            {
                logBuffer.Shutdown();
                installVm.Dispose();
                doctorVm.Dispose();
                storageVm.Dispose();
                environmentVm.Dispose();
                updatesVm.Dispose();
                historyVm.Dispose();
                updateCheck.Dispose();
                logService.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
