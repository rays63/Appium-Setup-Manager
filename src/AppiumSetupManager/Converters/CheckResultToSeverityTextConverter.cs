using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Maps a doctor CheckResult to its severity-tag label on the Doctor issue card: Fail → "Critical",
/// Warn → "Warning". Pass → "Fixed" is defensive only — a Pass check is filtered out of the
/// issue-card view entirely by CheckResultFilterConverter.NotPass, so this branch is not reachable
/// in the running UI, but is implemented per the design spec in case that filtering ever changes.
/// </summary>
public sealed class CheckResultToSeverityTextConverter : IValueConverter
{
    public static readonly CheckResultToSeverityTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CheckResult r
            ? r switch
            {
                CheckResult.Fail => Strings.DoctorSeverityCritical,
                CheckResult.Warn => Strings.DoctorSeverityWarning,
                CheckResult.Pass => Strings.DoctorSeverityFixed,
                _                => string.Empty,
            }
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
