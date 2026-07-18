using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Services;

namespace AppiumSetupManager.ViewModels;

public partial class CommandLogViewModel : ObservableObject
{
    // Channel draining lives in LogBuffer (the single reader of ILogService's channel);
    // this ViewModel only exposes the shared buffer plus the collapse/export chrome.
    public ReadOnlyObservableCollection<LogEntry> Entries { get; }

    [ObservableProperty]
    private bool _isCollapsed = false;

    public CommandLogViewModel(ILogBuffer buffer)
    {
        Entries = buffer.Entries;
    }

    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    private void ExportLog()
    {
        // TODO: Phase 6 — export log to file
    }
}
