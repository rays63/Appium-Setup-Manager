using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

public sealed class CheckResultToGlyphConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CheckResult r
            ? r switch
            {
                CheckResult.Pass => "✓",
                CheckResult.Warn => "⚠",
                CheckResult.Fail => "✗",
                _                => "?",
            }
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
