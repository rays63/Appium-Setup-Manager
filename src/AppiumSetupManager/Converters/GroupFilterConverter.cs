using System.Globalization;
using Avalonia.Data.Converters;

namespace AppiumSetupManager.Converters;

public sealed class GroupFilterConverter : IValueConverter
{
    public static readonly GroupFilterConverter Dependencies = new("Dependencies");
    public static readonly GroupFilterConverter Environment  = new("Environment");
    public static readonly GroupFilterConverter Drivers      = new("Drivers");
    public static readonly GroupFilterConverter Tools        = new("Tools");

    private readonly string _group;
    private GroupFilterConverter(string group) => _group = group;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s && s == _group;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
