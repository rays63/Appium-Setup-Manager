using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Services;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter reporting whether the bound ThemeMode equals the mode
/// named by ConverterParameter ("Light" or "Dark"). The app's nav/top-bar icons are
/// vector StreamGeometry resources rather than text glyphs, so a single-TextBlock glyph
/// swap (see ThemeModeToGlyphConverter) can't drive a Path.Data binding safely at
/// runtime — this sibling converter instead toggles IsVisible on two overlaid Path
/// icons (sun / moon), one per theme state, per Visual Redesign R1's icon spec.
/// </summary>
public sealed class ThemeModeEqualsConverter : IValueConverter
{
    public static readonly ThemeModeEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ThemeMode mode && parameter is string target && Enum.TryParse<ThemeMode>(target, out var targetMode)
            ? mode == targetMode
            : false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
