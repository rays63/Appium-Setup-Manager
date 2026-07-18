using System.Collections.ObjectModel;
using System.Threading.Channels;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Core.Services;

namespace AppiumSetupManager.Services;

public interface ILogBuffer
{
    /// <summary>All log entries drained so far, oldest first. Mutated only on the UI thread.</summary>
    ReadOnlyObservableCollection<LogEntry> Entries { get; }

    /// <summary>Stops the drain loop. Call once, at application shutdown.</summary>
    void Shutdown();
}

/// <summary>
/// The single drain point for <see cref="ILogService"/>'s log channel, marshaling entries onto
/// the UI thread into one shared observable collection that any number of ViewModels
/// (CommandLogViewModel, LogsViewModel, …) can observe.
///
/// INVARIANT: <see cref="ILogService.Entries"/> is a single-consumer ChannelReader — LogBuffer
/// must be its ONLY reader. A second reader would silently COMPETE for entries (each entry goes
/// to exactly one reader), not receive a broadcast copy. Never read the channel anywhere else;
/// consume this buffer's <see cref="Entries"/> instead.
///
/// Lives in the UI project (not Core) because marshaling requires Avalonia's Dispatcher.
/// </summary>
public sealed class LogBuffer : ILogBuffer
{
    private readonly ObservableCollection<LogEntry> _entries = new();
    private readonly CancellationTokenSource _cts = new();

    public LogBuffer(ILogService logService)
    {
        Entries = new ReadOnlyObservableCollection<LogEntry>(_entries);
        _ = Task.Run(() => DrainLoopAsync(logService.Entries, _cts.Token));
    }

    public ReadOnlyObservableCollection<LogEntry> Entries { get; }

    public void Shutdown() => _cts.Cancel();

    private async Task DrainLoopAsync(ChannelReader<LogEntry> reader, CancellationToken ct)
    {
        try
        {
            await foreach (var entry in reader.ReadAllAsync(ct))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => _entries.Add(entry));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown — swallow to avoid unobserved task exceptions.
        }
    }
}
