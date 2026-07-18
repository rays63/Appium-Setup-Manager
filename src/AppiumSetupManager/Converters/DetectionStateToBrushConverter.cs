using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

public sealed class DetectionStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush FoundBrush       = new(Color.Parse("#2ECC8A"));
    private static readonly SolidColorBrush OutdatedBrush    = new(Color.Parse("#F6A623"));
    private static readonly SolidColorBrush NotFoundBrush    = new(Color.Parse("#E05252"));
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DetectionState state)
            return null;

        return state switch
        {
            DetectionState.Found         => FoundBrush,
            DetectionState.Outdated      => OutdatedBrush,
            DetectionState.NotFound      => NotFoundBrush,
            DetectionState.NotApplicable => TransparentBrush,
            _                            => null,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
