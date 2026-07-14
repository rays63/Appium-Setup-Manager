using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Converts a bool IsCollapsed value to the appropriate glyph:
/// true  (collapsed) → expand arrow (▼)
/// false (expanded)  → collapse arrow (▲)
/// </summary>
public sealed class BoolToExpandCollapseConverter : IValueConverter
{
    public static readonly BoolToExpandCollapseConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Strings.CommandLogExpand : Strings.CommandLogCollapse;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
