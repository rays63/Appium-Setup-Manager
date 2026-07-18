using System.Globalization;
using Avalonia.Data.Converters;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Presentation-only converter that reports whether a bound <c>int</c> (typically an
/// <c>ObservableCollection&lt;T&gt;.Count</c>) is zero — used to drive an explicit
/// "nothing here" empty-state visual distinct from a loading state.
/// </summary>
public sealed class IsZeroConverter : IValueConverter
{
    public static readonly IsZeroConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i && i == 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
