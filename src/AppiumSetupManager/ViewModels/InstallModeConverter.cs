using System.Globalization;
using Avalonia.Data.Converters;

namespace AppiumSetupManager.ViewModels;

public sealed class InstallModeConverter : IValueConverter
{
    public static readonly InstallModeConverter Quick    = new(InstallMode.Quick);
    public static readonly InstallModeConverter Advanced = new(InstallMode.Advanced);

    private readonly InstallMode _target;
    private InstallModeConverter(InstallMode target) => _target = target;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallMode m && m == _target;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? _target : Avalonia.Data.BindingOperations.DoNothing;
}
