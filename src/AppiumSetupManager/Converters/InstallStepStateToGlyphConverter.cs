using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

public sealed class InstallStepStateToGlyphConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStepState state
            ? state switch
            {
                InstallStepState.Pending => "…",
                InstallStepState.Running => "↻",
                InstallStepState.Done    => "✓",
                InstallStepState.Failed  => "✗",
                InstallStepState.Skipped => "—",
                _                        => "?",
            }
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
