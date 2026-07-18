using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

public sealed class InstallStepStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Pending = new(Color.Parse("#888888"));
    private static readonly SolidColorBrush Running = new(Color.Parse("#4FC3F7")); // blue — matches command log command color
    private static readonly SolidColorBrush Done    = new(Color.Parse("#2ECC8A")); // green — matches dashboard Found
    private static readonly SolidColorBrush Failed  = new(Color.Parse("#E05252")); // red — matches dashboard NotFound
    private static readonly SolidColorBrush Skipped = new(Color.Parse("#555555")); // muted gray

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStepState state
            ? state switch
            {
                InstallStepState.Pending => Pending,
                InstallStepState.Running => Running,
                InstallStepState.Done    => Done,
                InstallStepState.Failed  => Failed,
                InstallStepState.Skipped => Skipped,
                _                        => Brushes.Transparent,
            }
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
