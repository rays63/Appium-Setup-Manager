using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;
using AppiumSetupManager.Localization;
using AppiumSetupManager.Services;
using Avalonia.Platform.Storage;

namespace AppiumSetupManager.ViewModels;

public partial class CommandLogViewModel : ObservableObject
{
    private readonly ILogService _logService;

    // Channel draining lives in LogBuffer (the single reader of ILogService's channel);
    // this ViewModel only exposes the shared buffer plus the collapse/export chrome.
    public ReadOnlyObservableCollection<LogEntry> Entries { get; }

    /// <summary>
    /// Save-file picker supplied by the view (CommandLogView code-behind), which owns the
    /// TopLevel/StorageProvider access this ViewModel must not take a dependency on. Receives the
    /// suggested file name (without extension); returns null when the user cancels the dialog.
    /// Same view→VM indirection style as DashboardViewModel.AdvancedInstallRequested, inverted
    /// into a delegate because the export needs the picker's async result back.
    /// </summary>
    public Func<string, Task<IStorageFile?>>? SaveFilePickerAsync { get; set; }

    [ObservableProperty]
    private bool _isCollapsed = false;

    public CommandLogViewModel(ILogBuffer buffer, ILogService logService)
    {
        Entries = buffer.Entries;
        _logService = logService;
    }

    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    // AsyncRelayCommand disallows concurrent executions by default, so the Export button is
    // automatically disabled while a previous export is still writing.
    [RelayCommand]
    private async Task ExportLogAsync()
    {
        if (SaveFilePickerAsync is null)
            return; // No view attached (e.g. during teardown) — nothing to export to.

        try
        {
            var suggestedName = string.Format(Strings.CommandLogExportFileNameFormat, DateTime.Now);
            var file = await SaveFilePickerAsync(suggestedName);
            if (file is null)
                return; // User cancelled the dialog.

            // Snapshot before writing: Entries keeps growing on the UI thread while the awaits
            // below yield, and enumerating a mutating collection would throw.
            var snapshot = Entries.ToList();

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            foreach (var entry in snapshot)
                await writer.WriteLineAsync(string.Format(Strings.CommandLogExportLineFormat, KindPrefix(entry.Kind), entry.Text));

            _logService.LogOutput(string.Format(Strings.CommandLogExportedFormat, file.Name), LogEntryKind.Info);
        }
        catch (Exception ex)
        {
            // Surface the failure in the console panel itself rather than crashing —
            // the log is the panel's own feedback channel.
            _logService.LogError(string.Format(Strings.CommandLogExportFailedFormat, ex.Message), ex);
        }
    }

    /// <summary>
    /// Per-line prefix so the exported plain-text file is self-describing. StdErr maps to [WRN]
    /// and Error to [ERR], matching the Warnings/Errors split LogsViewModel's filters use.
    /// </summary>
    private static string KindPrefix(LogEntryKind kind) => kind switch
    {
        LogEntryKind.Command => Strings.CommandLogExportKindCommand,
        LogEntryKind.StdOut  => Strings.CommandLogExportKindStdOut,
        LogEntryKind.StdErr  => Strings.CommandLogExportKindStdErr,
        LogEntryKind.Error   => Strings.CommandLogExportKindError,
        _                    => Strings.CommandLogExportKindInfo,
    };
}
