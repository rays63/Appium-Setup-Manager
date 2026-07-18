using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AppiumSetupManager.Controls;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Converts a single 0-100 percentage (e.g. DashboardViewModel.EnvironmentHealthPercent,
/// DoctorViewModel.HealthScorePercent) into a single-element RingChart.Segments list.
///
/// The arc color (#659287 / accent-600) is hardcoded rather than resolved from a DynamicResource
/// because Colors.axaml defines PrimaryContainerColor identically in both the Light and Dark
/// ResourceDictionary.ThemeDictionaries — so this is not a theme-reactivity regression, just a
/// literal restatement of a brush that never actually changes between themes. This mirrors the
/// existing precedent set by DetectionStateToBrushConverter/CheckResultToBrushConverter, which also
/// use fixed SolidColorBrush constants rather than live resource lookups.
/// </summary>
public sealed class PercentToRingSegmentsConverter : IValueConverter
{
    public static readonly PercentToRingSegmentsConverter Instance = new();

    private static readonly IBrush ValueBrush = new SolidColorBrush(Color.Parse("#659287"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percent = value switch
        {
            double d => d,
            int i => i,
            _ => 0d,
        };

        var fraction = Math.Clamp(percent / 100.0, 0, 1);
        return new List<RingSegment> { new(fraction, ValueBrush) };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
