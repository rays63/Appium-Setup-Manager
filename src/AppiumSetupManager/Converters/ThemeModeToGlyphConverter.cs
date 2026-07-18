using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Services;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter mapping the current ThemeMode to the glyph for a sun/moon
/// theme-toggle icon. Kept generic (plain glyph text out) so the AXAML pass can bind it
/// directly to an icon's Text, or repurpose the same enum value with a separate bool
/// converter to drive two-icon Visibility swapping instead.
/// </summary>
public sealed class ThemeModeToGlyphConverter : IValueConverter
{
    public static readonly ThemeModeToGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ThemeMode mode
            ? mode switch
            {
                ThemeMode.Light => "☀",
                ThemeMode.Dark  => "☾",
                _               => "?",
            }
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
