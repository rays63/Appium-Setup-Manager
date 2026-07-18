using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// Wraps a single <see cref="HistoryEntry"/> row on the History screen. Mirrors the callback-
/// delegation pattern used by EnvironmentBackupEntryViewModel — the actual rollback logic lives on
/// the host HistoryViewModel (which also owns <see cref="IsRestoring"/>'s lifecycle), this just
/// relays the click. Only Setup entries with a rollback target ever show the Rollback button;
/// Install/Update/Repair/Cleanup rows are permanent log lines with no inverse operation.
/// </summary>
public partial class HistoryEntryViewModel : ObservableObject
{
    private readonly Func<HistoryEntryViewModel, Task>? _rollbackHandler;

    /// <summary>The underlying Core model — needed by HistoryViewModel to perform the rollback.</summary>
    public HistoryEntry Model { get; }

    /// <summary>Local wall-clock time of the action, e.g. "3:42 PM".</summary>
    public string TimeText { get; }

    /// <summary>Localized label rendered inside the row's outlined type tag.</summary>
    public string TypeTagText { get; }

    public string Title => Model.Title;

    public string? Summary => Model.Summary;

    public bool HasSummary => !string.IsNullOrWhiteSpace(Model.Summary);

    public bool IsRollbackCapable => Model.IsRollbackCapable;

    [ObservableProperty]
    private bool _isRestoring;

    public HistoryEntryViewModel(HistoryEntry entry, Func<HistoryEntryViewModel, Task>? rollbackHandler = null)
    {
        Model = entry;
        _rollbackHandler = rollbackHandler;
        TimeText = entry.CreatedAtUtc.ToLocalTime().ToString("h:mm tt");
        TypeTagText = entry.Type switch
        {
            HistoryEntryType.Install => Strings.HistoryTypeInstall,
            HistoryEntryType.Update  => Strings.HistoryTypeUpdate,
            HistoryEntryType.Repair  => Strings.HistoryTypeRepair,
            HistoryEntryType.Cleanup => Strings.HistoryTypeCleanup,
            _                        => Strings.HistoryTypeSetup,
        };
    }

    [RelayCommand]
    private async Task RollbackAsync()
    {
        if (_rollbackHandler is null || IsRestoring)
            return;

        await _rollbackHandler(this);
    }
}
