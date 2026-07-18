using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// Backs the History screen: every action this app has taken (installs, updates, repairs, cleanups,
/// setup changes), grouped into day cards, newest day first. Only Setup entries with a recorded
/// environment-variable backup are rollback-capable — everything else is a permanent log line, so
/// no Rollback affordance is ever fabricated for it.
/// </summary>
public partial class HistoryViewModel : ObservableObject, IDisposable
{
    private readonly IHistoryService _history;
    private readonly IEnvironmentBackupStore _backupStore;
    private readonly IEnvironmentVariableManager _envManager;
    private CancellationTokenSource _loadCts = new();

    public ObservableCollection<HistoryDayGroupViewModel> DayGroups { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    public bool HasEntries => DayGroups.Count > 0;

    public HistoryViewModel(IHistoryService history, IEnvironmentBackupStore backupStore, IEnvironmentVariableManager envManager)
    {
        _history     = history;
        _backupStore = backupStore;
        _envManager  = envManager;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        try
        {
            var entries = await _history.ListAsync(ct);

            DayGroups.Clear();
            // ListAsync is already newest-first, so within each local-calendar-day group the rows
            // keep that order; the groups themselves are ordered newest day first.
            foreach (var group in entries
                         .GroupBy(e => e.CreatedAtUtc.ToLocalTime().Date)
                         .OrderByDescending(g => g.Key))
            {
                var rows = group.Select(e => new HistoryEntryViewModel(e, RollbackEntryAsync));
                DayGroups.Add(new HistoryDayGroupViewModel(FormatDayLabel(group.Key), rows));
            }

            OnPropertyChanged(nameof(HasEntries));
        }
        catch (OperationCanceledException)
        {
            // Non-fatal — leave whatever was loaded before cancellation in place.
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// The callback handed to every rollback-capable HistoryEntryViewModel. Resolves the entry's
    /// backup target and writes the backed-up value back via the same restore mechanism the
    /// Environment screen uses. A stale target (backup pruned from the bounded store) is a quiet
    /// no-op rather than an error — there is nothing left to restore.
    /// </summary>
    private async Task RollbackEntryAsync(HistoryEntryViewModel entryVm)
    {
        var targetId = entryVm.Model.RollbackTargetId;
        if (targetId is null)
            return;

        var ct = _loadCts.Token;
        var rolledBack = false;

        entryVm.IsRestoring = true;
        try
        {
            var backup = await _backupStore.FindAsync(targetId, ct);
            if (backup is null)
                return;

            await _envManager.SetUserAsync(backup.VariableName, backup.PreviousValue, ct);
            rolledBack = true;

            try { await _history.RecordAsync(HistoryEntryType.Setup, $"Restored {backup.VariableName}", backup.PreviousValue, null, ct); }
            catch { /* swallow — non-fatal history-write failure */ }
        }
        finally
        {
            entryVm.IsRestoring = false;
        }

        if (rolledBack)
            await LoadAsync();
    }

    private static string FormatDayLabel(DateTime localDate)
    {
        var today = DateTime.Now.Date;
        if (localDate == today)
            return Strings.HistoryDayToday;
        if (localDate == today.AddDays(-1))
            return Strings.HistoryDayYesterday;
        return localDate.ToString("MMM d, yyyy");
    }

    public void Dispose()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
    }
}
