using System.Globalization;
using Avalonia.Data.Converters;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter that uppercases a string for label-caps styling.
/// </summary>
public sealed class ToUpperConverter : IValueConverter
{
    public static readonly ToUpperConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? s.ToUpper(culture) : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
