using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Short status-pill label for a completed install step — Done vs. Skipped — used by the
/// Completed row's sage tag on the Installation flow-graph. Companion to the existing
/// InstallStepStateToGlyphConverter/ToBrushConverter (icon/color); this supplies the accessible
/// text label so the tag never communicates state via color alone.
/// </summary>
public sealed class InstallStepStateToLabelConverter : IValueConverter
{
    public static readonly InstallStepStateToLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStepState s
            ? s switch
            {
                InstallStepState.Done    => Strings.InstallStepDone,
                InstallStepState.Skipped => Strings.InstallStepSkipped,
                InstallStepState.Failed  => Strings.InstallFailedGroup,
                InstallStepState.Running => Strings.InstallActiveGroup,
                InstallStepState.Pending => Strings.InstallQueuedGroup,
                _                        => string.Empty,
            }
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
