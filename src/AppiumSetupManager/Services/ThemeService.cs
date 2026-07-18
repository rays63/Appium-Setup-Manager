using Avalonia;
using Avalonia.Styling;

namespace AppiumSetupManager.Services;

/// <summary>
/// The two theme variants the shell supports. Visual Redesign R1 adds the light/dark toggle;
/// "Phase" terminology is reserved for the unrelated functional milestones in
/// PROJECT_DOCUMENTS.md, so this feature is referred to as "Visual Redesign R1" in comments.
/// </summary>
public enum ThemeMode
{
    Light,
    Dark,
}

public interface IThemeService
{
    ThemeMode CurrentMode { get; }

    /// <summary>Raised after CurrentMode changes and the new variant has been applied.</summary>
    event EventHandler<ThemeMode>? ThemeChanged;

    void SetMode(ThemeMode mode);
}

/// <summary>
/// Applies the requested theme variant to the running Application and remembers the
/// current selection. Persistence is delegated to IThemePreferenceStore — a later phase
/// swaps InMemoryThemePreferenceStore for a real file-backed store without this class
/// needing to change.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly IThemePreferenceStore _preferenceStore;

    public ThemeService(IThemePreferenceStore preferenceStore)
    {
        _preferenceStore = preferenceStore;
        CurrentMode       = _preferenceStore.Load();
        Apply(CurrentMode);
    }

    public ThemeMode CurrentMode { get; private set; }

    public event EventHandler<ThemeMode>? ThemeChanged;

    public void SetMode(ThemeMode mode)
    {
        if (CurrentMode == mode)
            return;

        CurrentMode = mode;
        Apply(mode);
        _preferenceStore.Save(mode);
        ThemeChanged?.Invoke(this, mode);
    }

    private static void Apply(ThemeMode mode) =>
        Application.Current!.RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark  => ThemeVariant.Dark,
            _               => ThemeVariant.Dark,
        };
}
