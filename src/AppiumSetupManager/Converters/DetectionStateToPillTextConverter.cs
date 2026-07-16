using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter mapping a DetectionState to a short status-pill label.
/// Found → INSTALLED, Outdated → OUTDATED, NotFound → MISSING.
/// </summary>
public sealed class DetectionStateToPillTextConverter : IValueConverter
{
    public static readonly DetectionStateToPillTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DetectionState state
            ? state switch
            {
                DetectionState.Found    => Strings.PillInstalled,
                DetectionState.Outdated => Strings.PillOutdated,
                DetectionState.NotFound => Strings.PillMissing,
                _                       => string.Empty,
            }
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
