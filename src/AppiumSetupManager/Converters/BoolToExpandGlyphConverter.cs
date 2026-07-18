using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.Converters;

public sealed class BoolToExpandGlyphConverter : IValueConverter
{
    public static readonly BoolToExpandGlyphConverter Instance = new();
    private BoolToExpandGlyphConverter() { }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Strings.InstallCollapseCommand : Strings.InstallExpandCommand;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
