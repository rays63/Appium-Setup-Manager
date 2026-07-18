using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Services;

/// <summary>
/// File-backed theme preference, implemented as a thin adapter over <see cref="ISettingsStore"/>:
/// maps the UI's <see cref="ThemeMode"/> to Core's persisted <see cref="AppThemeMode"/> so
/// ThemeService's constructor shape never changes (the seam InMemoryThemePreferenceStore
/// reserved for exactly this swap).
/// </summary>
public sealed class SettingsThemePreferenceStore(ISettingsStore settings) : IThemePreferenceStore
{
    public ThemeMode Load() => settings.Current.Theme switch
    {
        AppThemeMode.Light => ThemeMode.Light,
        _                  => ThemeMode.Dark,
    };

    public void Save(ThemeMode mode) =>
        settings.Update(s => s with
        {
            Theme = mode == ThemeMode.Light ? AppThemeMode.Light : AppThemeMode.Dark,
        });
}
