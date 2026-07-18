using System;
using System.Globalization;
using Avalonia.Data.Converters;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.Converters;

/// <summary>
/// Buckets an InstallStepState into one of the four flow-graph rows on the Installation screen:
/// Completed (Done or Skipped), Active (Running), Queued (Pending), Failed. Pure AXAML IsVisible
/// filtering, same pattern as GroupFilterConverter.
/// </summary>
public sealed class InstallStepStateFilterConverter : IValueConverter
{
    public static readonly InstallStepStateFilterConverter Completed =
        new(s => s is InstallStepState.Done or InstallStepState.Skipped);

    public static readonly InstallStepStateFilterConverter Active =
        new(s => s == InstallStepState.Running);

    public static readonly InstallStepStateFilterConverter Queued =
        new(s => s == InstallStepState.Pending);

    public static readonly InstallStepStateFilterConverter Failed =
        new(s => s == InstallStepState.Failed);

    private readonly Func<InstallStepState, bool> _predicate;
    private InstallStepStateFilterConverter(Func<InstallStepState, bool> predicate) => _predicate = predicate;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStepState s && _predicate(s);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
