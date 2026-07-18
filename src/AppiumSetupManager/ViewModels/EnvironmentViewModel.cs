using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Infrastructure;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// Backs the Environment screen: hand-editable JAVA_HOME/ANDROID_HOME rows, a read-only toolchain
/// location list, and a history of backed-up variable values with one-click restore. Detailed
/// JAVA_HOME/ANDROID_HOME editing used to live on the Dashboard's "Environment Details" section —
/// that section has been removed and its capability rebuilt here (see engineering report).
/// </summary>
public partial class EnvironmentViewModel : ObservableObject, IDisposable
{
    // JAVA_HOME/ANDROID_HOME only — PATH isn't meaningfully hand-editable as a single value here.
    private static readonly string[] EditableVariableNames = ["JAVA_HOME", "ANDROID_HOME"];

    private readonly IDetectionService _detection;
    private readonly IEnvironmentVariableManager _envManager;
    private readonly IEnvironmentBackupStore _backupStore;
    private readonly IHistoryService _history;
    private CancellationTokenSource _loadCts = new();

    public ObservableCollection<EnvironmentVariableRowViewModel> VariableRows { get; } = new();
    public ObservableCollection<ToolchainRowViewModel> ToolchainRows { get; } = new();
    public ObservableCollection<EnvironmentBackupEntryViewModel> BackupEntries { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSaving;

    public bool HasBackupEntries => BackupEntries.Count > 0;

    public EnvironmentViewModel(IDetectionService detection, IEnvironmentVariableManager envManager, IEnvironmentBackupStore backupStore, IHistoryService history)
    {
        _detection  = detection;
        _envManager = envManager;
        _backupStore = backupStore;
        _history = history;
        _ = LoadAsync();
    }

    private bool CanSaveChanges() => !IsSaving && VariableRows.Any(r => r.IsDirty);

    /// <summary>
    /// Persists every dirty row. For a row whose original value was non-empty, the previous value is
    /// backed up first so it can be restored later; a row that was previously unset has nothing
    /// meaningful to restore to, so it is just written directly.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private async Task SaveChangesAsync()
    {
        IsSaving = true;
        SaveChangesCommand.NotifyCanExecuteChanged();

        try
        {
            foreach (var row in VariableRows.Where(r => r.IsDirty).ToList())
            {
                // A row whose original value was empty has nothing meaningful to back up (and
                // therefore nothing to roll back to), but it's still a real Setup action worth
                // logging — just without a rollback target.
                string? backupId = null;
                if (!string.IsNullOrEmpty(row.OriginalValue))
                {
                    await _backupStore.RecordAsync(row.Name, row.OriginalValue, Strings.EnvironmentManualEditReason);
                    backupId = await FindLatestBackupIdAsync(row.Name);
                }

                await _envManager.SetUserAsync(row.Name, row.EditedValue);

                await _history.RecordAsync(HistoryEntryType.Setup, $"Updated {row.Name}", row.EditedValue, backupId);
            }
        }
        finally
        {
            IsSaving = false;
        }

        await LoadAsync();
    }

    /// <summary>
    /// The callback handed to every EnvironmentBackupEntryViewModel. Records the variable's *current*
    /// value first (so the restore itself is reversible from a later backup entry), then writes the
    /// entry's previous value back, then reloads so every row/list reflects the new persisted state.
    /// </summary>
    private async Task RestoreEntryAsync(EnvironmentBackupEntryViewModel entryVm)
    {
        var entry = entryVm.Model;
        var currentValue = Environment.GetEnvironmentVariable(entry.VariableName) ?? string.Empty;

        await _backupStore.RecordAsync(entry.VariableName, currentValue, Strings.EnvironmentRestoreReason);
        var backupId = await FindLatestBackupIdAsync(entry.VariableName);
        await _envManager.SetUserAsync(entry.VariableName, entry.PreviousValue);

        await _history.RecordAsync(HistoryEntryType.Setup, $"Restored {entry.VariableName}", entry.PreviousValue, backupId);

        await LoadAsync();
    }

    /// <summary>
    /// Looks up the Id of the most recently recorded backup entry for a variable. Backups are
    /// newest-first, and the backup for this exact save/restore was just written, so the first match
    /// is reliably the one that corresponds to the action just taken.
    /// </summary>
    private async Task<string?> FindLatestBackupIdAsync(string variableName)
    {
        var backups = await _backupStore.ListAsync();
        return backups.FirstOrDefault(e => e.VariableName == variableName)?.Id;
    }

    private async Task LoadAsync()
    {
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        try
        {
            var results = await _detection.ScanAllAsync(ct);
            var backups = await _backupStore.ListAsync(ct);

            VariableRows.Clear();
            foreach (var name in EditableVariableNames)
            {
                var value = Environment.GetEnvironmentVariable(name) ?? string.Empty;
                var row = new EnvironmentVariableRowViewModel(name, value);
                row.PropertyChanged += OnVariableRowPropertyChanged;
                VariableRows.Add(row);
            }

            var node        = results.FirstOrDefault(r => r.Name == "Node.js");
            var npm         = results.FirstOrDefault(r => r.Name == "npm");
            var jdk         = results.FirstOrDefault(r => r.Name == "JDK");
            var javaHome    = results.FirstOrDefault(r => r.Name == "JAVA_HOME");
            var adb         = results.FirstOrDefault(r => r.Name == "ADB");
            var androidHome = results.FirstOrDefault(r => r.Name == "ANDROID_HOME");

            ToolchainRows.Clear();
            ToolchainRows.Add(new ToolchainRowViewModel(Strings.EnvironmentToolchainNode, FormatPath(node?.InstallPath), FormatVersion(node?.InstalledVersion)));
            ToolchainRows.Add(new ToolchainRowViewModel(Strings.EnvironmentToolchainNpm, FormatPath(npm?.InstallPath), FormatVersion(npm?.InstalledVersion)));
            ToolchainRows.Add(new ToolchainRowViewModel(Strings.EnvironmentToolchainJava, FormatPath(javaHome?.InstallPath), FormatVersion(jdk?.InstalledVersion)));
            ToolchainRows.Add(new ToolchainRowViewModel(Strings.EnvironmentToolchainAdb, FormatPath(adb?.InstallPath), FormatVersion(adb?.InstalledVersion)));
            // Android SDK has no single version in current detection data — don't fabricate one.
            ToolchainRows.Add(new ToolchainRowViewModel(Strings.EnvironmentToolchainSdk, FormatPath(androidHome?.InstallPath), Strings.EnvironmentNoSingleVersion));

            BackupEntries.Clear();
            foreach (var entry in backups)
                BackupEntries.Add(new EnvironmentBackupEntryViewModel(entry, RestoreEntryAsync));
            OnPropertyChanged(nameof(HasBackupEntries));
        }
        catch (OperationCanceledException)
        {
            // Non-fatal — leave whatever was loaded before cancellation in place.
        }
        finally
        {
            IsLoading = false;
            SaveChangesCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnVariableRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EnvironmentVariableRowViewModel.IsDirty))
            SaveChangesCommand.NotifyCanExecuteChanged();
    }

    private static string FormatPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? Strings.DashboardNotDetected : path;

    private static string FormatVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? Strings.EnvironmentNoSingleVersion : version;

    public void Dispose()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
    }
}
