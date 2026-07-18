namespace AppiumSetupManager.Services;

public interface IThemePreferenceStore
{
    ThemeMode Load();
    void Save(ThemeMode mode);
}
