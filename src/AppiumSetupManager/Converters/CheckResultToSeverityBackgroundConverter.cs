using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Maps a doctor CheckResult to the matching pill background brush already defined per-theme in
/// Themes/Colors.axaml (PillError/Warn/InstalledBackgroundBrush) — reuses those tokens rather than
/// inventing new colors, per the design spec.
///
/// Resolved via Application.Current.TryGetResource(key, ActualThemeVariant, ...) at convert time,
/// which is correct on initial render and after any Result-changing update (e.g. re-running doctor
/// checks). Known limitation: because this is a plain IValueConverter (not a DynamicResource
/// binding), the resolved brush will not repaint automatically if the user toggles the app theme
/// without the underlying Result also changing — the same limitation already present in
/// DetectionStateToPillBackgroundConverter, which uses theme-invariant fixed tints for the same
/// reason.
/// </summary>
public sealed class CheckResultToSeverityBackgroundConverter : IValueConverter
{
    public static readonly CheckResultToSeverityBackgroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is CheckResult r
            ? r switch
            {
                CheckResult.Fail => "PillErrorBackgroundBrush",
                CheckResult.Warn => "PillWarnBackgroundBrush",
                CheckResult.Pass => "PillInstalledBackgroundBrush",
                _ => null,
            }
            : null;

        if (key is null)
            return Brushes.Transparent;

        var app = Application.Current;
        if (app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
