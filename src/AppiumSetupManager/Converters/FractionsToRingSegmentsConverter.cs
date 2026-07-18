using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AppiumSetupManager.Controls;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Converts a sequence of 0..1 fractions (StorageViewModel.CategoryFractions — one per storage
/// category, each category's share of total reclaimable bytes) into a multi-segment
/// RingChart.Segments list, cycling through the sage accent ramp documented at the top of
/// Themes/Colors.axaml (accent-300/500/600/700/800) so added colors stay within the established
/// palette rather than inventing new ones.
/// </summary>
public sealed class FractionsToRingSegmentsConverter : IValueConverter
{
    public static readonly FractionsToRingSegmentsConverter Instance = new();

    private static readonly IReadOnlyList<IBrush> Palette =
    [
        new SolidColorBrush(Color.Parse("#659287")), // accent-600
        new SolidColorBrush(Color.Parse("#88BDA4")), // accent-500
        new SolidColorBrush(Color.Parse("#51756C")), // accent-700
        new SolidColorBrush(Color.Parse("#B1D3B9")), // accent-300
        new SolidColorBrush(Color.Parse("#425F58")), // accent-800
    ];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable enumerable)
            return null;

        var fractions = enumerable.Cast<object>().Select(o => System.Convert.ToDouble(o, culture)).ToList();
        var segments = new List<RingSegment>(fractions.Count);
        for (var i = 0; i < fractions.Count; i++)
            segments.Add(new RingSegment(fractions[i], Palette[i % Palette.Count]));

        return segments;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
