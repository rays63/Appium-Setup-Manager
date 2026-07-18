using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using AppiumSetupManager.ViewModels;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter mapping LogsViewModel.SelectedFilter to a filter-pill
/// RadioButton IsChecked bool. Two-way: when a pill becomes checked it writes its
/// ConverterParameter's LogFilter back; unchecking is ignored (DoNothing) so the
/// RadioButton GroupName owns single-selection — same pattern as NavActiveConverter.
/// </summary>
public sealed class LogFilterEqualsConverter : IValueConverter
{
    public static readonly LogFilterEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is LogFilter current
        && parameter is string target
        && Enum.TryParse<LogFilter>(target, out var targetFilter)
        && current == targetFilter;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string target && Enum.TryParse<LogFilter>(target, out var targetFilter)
            ? targetFilter
            : BindingOperations.DoNothing;
}
