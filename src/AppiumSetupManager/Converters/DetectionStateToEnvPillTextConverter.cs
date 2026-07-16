using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter mapping a DetectionState to the "environment variable" status-pill
/// wording used on the Environment Configuration dashboard — Found → VERIFIED, NotFound → MISSING,
/// Outdated → WARNING. Distinct from DetectionStateToPillTextConverter's INSTALLED/OUTDATED/MISSING
/// wording, which reads oddly for an env var (an env var isn't "installed").
/// </summary>
public sealed class DetectionStateToEnvPillTextConverter : IValueConverter
{
    public static readonly DetectionStateToEnvPillTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DetectionState state
            ? state switch
            {
                DetectionState.Found    => Strings.DashboardEnvVerified,
                DetectionState.Outdated => Strings.DashboardEnvWarning,
                DetectionState.NotFound => Strings.DashboardEnvMissing,
                _                       => string.Empty,
            }
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
