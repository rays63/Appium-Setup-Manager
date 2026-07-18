using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Splits DoctorCheckViewModel.Result into Pass (compact single-line CheckCard row) vs. NotPass
/// (Warn/Fail — full issue-card treatment). Pure AXAML IsVisible filtering, same pattern as
/// GroupFilterConverter.
/// </summary>
public sealed class CheckResultFilterConverter : IValueConverter
{
    public static readonly CheckResultFilterConverter Pass = new(r => r == CheckResult.Pass);
    public static readonly CheckResultFilterConverter NotPass = new(r => r != CheckResult.Pass);

    private readonly Func<CheckResult, bool> _predicate;
    private CheckResultFilterConverter(Func<CheckResult, bool> predicate) => _predicate = predicate;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is CheckResult r && _predicate(r);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
