using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// Wraps a single <see cref="EnvironmentBackupEntry"/> for the "Backups &amp; Restore Points" list,
/// exposing a human-readable "time · reason" line and a Restore action. Mirrors the callback-
/// delegation pattern used by ComponentStatusViewModel/StorageItemViewModel — the actual restore
/// logic lives on the host EnvironmentViewModel, this just relays the click.
/// </summary>
public partial class EnvironmentBackupEntryViewModel : ObservableObject
{
    private readonly Func<EnvironmentBackupEntryViewModel, Task>? _restoreHandler;

    /// <summary>The underlying Core model — needed by EnvironmentViewModel to perform the restore.</summary>
    public EnvironmentBackupEntry Model { get; }

    public string VariableName => Model.VariableName;

    /// <summary>e.g. "2h ago · Manual edit from Environment screen".</summary>
    public string DisplayText { get; }

    [ObservableProperty]
    private bool _isRestoring;

    public EnvironmentBackupEntryViewModel(EnvironmentBackupEntry entry, Func<EnvironmentBackupEntryViewModel, Task>? restoreHandler = null)
    {
        Model = entry;
        _restoreHandler = restoreHandler;
        DisplayText = string.Format(Strings.EnvironmentTimeReasonFormat, FormatRelativeTime(entry.CreatedAtUtc), entry.Reason);
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (_restoreHandler is null || IsRestoring)
            return;

        IsRestoring = true;
        try
        {
            await _restoreHandler(this);
        }
        finally
        {
            IsRestoring = false;
        }
    }

    private static string FormatRelativeTime(DateTimeOffset createdAtUtc)
    {
        var delta = DateTimeOffset.UtcNow - createdAtUtc;

        if (delta < TimeSpan.FromMinutes(1))
            return Strings.EnvironmentJustNow;
        if (delta < TimeSpan.FromHours(1))
            return string.Format(Strings.EnvironmentMinutesAgoFormat, (int)delta.TotalMinutes);
        if (delta < TimeSpan.FromHours(24))
            return string.Format(Strings.EnvironmentHoursAgoFormat, (int)delta.TotalHours);
        if (delta < TimeSpan.FromDays(7))
            return string.Format(Strings.EnvironmentDaysAgoFormat, (int)delta.TotalDays);

        // Older than a week — an absolute local timestamp is more useful than "9d ago".
        return createdAtUtc.ToLocalTime().ToString("MMM d, yyyy HH:mm");
    }
}
