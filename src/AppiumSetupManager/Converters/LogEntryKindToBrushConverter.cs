using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

public sealed class LogEntryKindToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush CommandBrush = new(Color.Parse("#4FC3F7"));
    private static readonly SolidColorBrush StdErrBrush  = new(Color.Parse("#FFB300"));
    private static readonly SolidColorBrush ErrorBrush   = new(Color.Parse("#EF5350"));
    private static readonly SolidColorBrush InfoBrush    = new(Color.Parse("#98cbff")); // muted azure — reads on near-black
    private static readonly SolidColorBrush StdOutBrush  = new(Color.Parse("#bec7d4")); // on-surface-variant

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogEntryKind kind)
            return StdOutBrush;

        return kind switch
        {
            LogEntryKind.Command => CommandBrush,
            LogEntryKind.StdErr  => StdErrBrush,
            LogEntryKind.Error   => ErrorBrush,
            LogEntryKind.Info    => InfoBrush,
            _                    => StdOutBrush,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
