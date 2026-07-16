using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

public sealed class CheckResultToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Pass = new(Color.Parse("#2ECC8A"));
    private static readonly SolidColorBrush Warn = new(Color.Parse("#F6A623"));
    private static readonly SolidColorBrush Fail = new(Color.Parse("#E05252"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CheckResult r
            ? r switch
            {
                CheckResult.Pass => Pass,
                CheckResult.Warn => Warn,
                CheckResult.Fail => Fail,
                _                => Brushes.Transparent,
            }
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
