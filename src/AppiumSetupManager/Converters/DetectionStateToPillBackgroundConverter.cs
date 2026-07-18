using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter mapping a DetectionState to a low-opacity pill
/// background tint. Tints mirror the Midnight Slate accents in Themes/Colors.axaml.
/// </summary>
public sealed class DetectionStateToPillBackgroundConverter : IValueConverter
{
    public static readonly DetectionStateToPillBackgroundConverter Instance = new();

    private static readonly SolidColorBrush InstalledTint = new(Color.Parse("#244edea3"));
    private static readonly SolidColorBrush OutdatedTint  = new(Color.Parse("#24ffb95f"));
    private static readonly SolidColorBrush MissingTint   = new(Color.Parse("#24ffb4ab"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DetectionState state
            ? state switch
            {
                DetectionState.Found    => InstalledTint,
                DetectionState.Outdated => OutdatedTint,
                DetectionState.NotFound => MissingTint,
                _                       => Brushes.Transparent,
            }
            : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
