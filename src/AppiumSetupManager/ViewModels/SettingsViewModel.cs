using CommunityToolkit.Mvvm.ComponentModel;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Platform;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// Backs the Settings screen: three persisted preference toggles (write-through to
/// <see cref="ISettingsStore"/> on every change) plus a read-only, platform-aware list of
/// keyboard shortcuts. The theme preference is deliberately not exposed here — the top-bar
/// toggle owns it via ThemeService/SettingsThemePreferenceStore. No telemetry of any kind
/// exists; everything the app records stays in local files (see the Privacy card).
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;

    [ObservableProperty]
    private bool _automaticUpdateChecks;

    [ObservableProperty]
    private bool _notifyOnFailure;

    [ObservableProperty]
    private bool _notifyOnCompletion;

    public IReadOnlyList<ShortcutRowViewModel> Shortcuts { get; }

    public SettingsViewModel(ISettingsStore settings, IPlatformAdapter platform)
    {
        _settings = settings;

        var current = settings.Current;
        _automaticUpdateChecks = current.AutomaticUpdateChecks;
        _notifyOnFailure       = current.NotifyOnFailure;
        _notifyOnCompletion    = current.NotifyOnCompletion;

        Shortcuts = platform.IsMacOs
            ?
            [
                new ShortcutRowViewModel(Strings.SettingsShortcutQuickInstall, Strings.SettingsBadgeMacQuickInstall),
                new ShortcutRowViewModel(Strings.SettingsShortcutOpenConsole,  Strings.SettingsBadgeMacOpenConsole),
                new ShortcutRowViewModel(Strings.SettingsShortcutRunDoctor,    Strings.SettingsBadgeMacRunDoctor),
                new ShortcutRowViewModel(Strings.SettingsShortcutFocusSearch,  Strings.SettingsBadgeMacFocusSearch),
                new ShortcutRowViewModel(Strings.SettingsShortcutToggleTheme,  Strings.SettingsBadgeMacToggleTheme),
            ]
            :
            [
                new ShortcutRowViewModel(Strings.SettingsShortcutQuickInstall, Strings.SettingsBadgeWinQuickInstall),
                new ShortcutRowViewModel(Strings.SettingsShortcutOpenConsole,  Strings.SettingsBadgeWinOpenConsole),
                new ShortcutRowViewModel(Strings.SettingsShortcutRunDoctor,    Strings.SettingsBadgeWinRunDoctor),
                new ShortcutRowViewModel(Strings.SettingsShortcutFocusSearch,  Strings.SettingsBadgeWinFocusSearch),
                new ShortcutRowViewModel(Strings.SettingsShortcutToggleTheme,  Strings.SettingsBadgeWinToggleTheme),
            ];
    }

    partial void OnAutomaticUpdateChecksChanged(bool value) =>
        _settings.Update(s => s with { AutomaticUpdateChecks = value });

    partial void OnNotifyOnFailureChanged(bool value) =>
        _settings.Update(s => s with { NotifyOnFailure = value });

    partial void OnNotifyOnCompletionChanged(bool value) =>
        _settings.Update(s => s with { NotifyOnCompletion = value });
}
