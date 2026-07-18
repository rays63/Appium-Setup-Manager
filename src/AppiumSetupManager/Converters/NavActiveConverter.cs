using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter that maps the MainWindowViewModel.ActiveNav string
/// to a RadioButton IsChecked bool. Two-way: when a radio becomes checked it writes
/// its ConverterParameter back into ActiveNav; unchecking is ignored (DoNothing) so
/// the RadioButton GroupName owns single-selection.
/// </summary>
public sealed class NavActiveConverter : IValueConverter
{
    public static readonly NavActiveConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string current && current == parameter as string;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string target ? target : BindingOperations.DoNothing;
}
