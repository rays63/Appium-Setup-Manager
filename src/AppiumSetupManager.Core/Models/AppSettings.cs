using System.Text.Json.Serialization;

namespace AppiumSetupManager.Core.Models;

/// <summary>
/// Persisted theme preference. Core deliberately owns its own enum rather than referencing the
/// UI project's ThemeMode — the UI maps between the two at its ThemeService seam.
/// Serialized as a string ("Dark"/"Light") for a human-readable settings file.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AppThemeMode { Dark, Light }

/// <summary>
/// The full set of user preferences persisted to settings.json. Immutable — mutate via
/// <c>record with</c> expressions through ISettingsStore.Update.
/// </summary>
public sealed record AppSettings
{
    public AppThemeMode Theme { get; init; } = AppThemeMode.Dark;
    public bool AutomaticUpdateChecks { get; init; } = true;
    public bool NotifyOnFailure { get; init; } = true;
    public bool NotifyOnCompletion { get; init; } = true;
    public bool AnonymousUsageData { get; init; } = false;
}
