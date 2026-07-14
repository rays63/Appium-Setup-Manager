using System.Collections.ObjectModel;
using System.Threading.Channels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;

namespace AppiumSetupManager.ViewModels;

public partial class CommandLogViewModel : ObservableObject
{
    public ObservableCollection<LogEntry> Entries { get; } = new();

    [ObservableProperty]
    private bool _isCollapsed = false;

    private readonly CancellationTokenSource _cts = new();

    public CommandLogViewModel(ILogService logService)
    {
        _ = Task.Run(() => DrainLoopAsync(logService.Entries, _cts.Token));
    }

    public void Shutdown() => _cts.Cancel();

    [RelayCommand]
    private void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    private void ExportLog()
    {
        // TODO: Phase 6 — export log to file
    }

    private async Task DrainLoopAsync(ChannelReader<LogEntry> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var entry in reader.ReadAllAsync(ct))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Entries.Add(entry));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown — swallow to avoid unobserved task exceptions.
        }
    }
}
