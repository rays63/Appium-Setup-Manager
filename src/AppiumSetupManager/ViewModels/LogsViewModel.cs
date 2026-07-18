using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using AppiumSetupManager.Core.Models;
using AppiumSetupManager.Services;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// UI-side filter categories for the Logs screen. Mapping to <see cref="LogEntryKind"/> (all five
/// members covered): Commands → Command; Output → StdOut and Info; Warnings → StdErr;
/// Errors → Error.
/// </summary>
public enum LogFilter { All, Commands, Output, Warnings, Errors }

/// <summary>
/// Backs the Logs screen: the full command history from the shared <see cref="ILogBuffer"/>,
/// narrowed by a kind filter and a case-insensitive substring search.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private readonly ReadOnlyObservableCollection<LogEntry> _source;

    /// <summary>Entries matching the current filter + search, oldest first.</summary>
    public ObservableCollection<LogEntry> FilteredEntries { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private LogFilter _selectedFilter = LogFilter.All;

    public bool HasVisibleEntries => FilteredEntries.Count > 0;

    public LogsViewModel(ILogBuffer buffer)
    {
        _source = buffer.Entries;
        ((INotifyCollectionChanged)_source).CollectionChanged += OnSourceCollectionChanged;
        Rebuild();
    }

    partial void OnSearchTextChanged(string value) => Rebuild();

    partial void OnSelectedFilterChanged(LogFilter value) => Rebuild();

    // LogBuffer mutates its collection exclusively on the UI thread, so this handler (and the
    // rebuilds above, triggered by UI-thread property setters) never race each other.
    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            // Fast path: the buffer is append-only in practice, so a matching new entry can be
            // appended without re-filtering the whole history.
            foreach (LogEntry entry in e.NewItems)
            {
                if (Matches(entry))
                    FilteredEntries.Add(entry);
            }

            OnPropertyChanged(nameof(HasVisibleEntries));
        }
        else
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        FilteredEntries.Clear();
        foreach (var entry in _source)
        {
            if (Matches(entry))
                FilteredEntries.Add(entry);
        }

        OnPropertyChanged(nameof(HasVisibleEntries));
    }

    private bool Matches(LogEntry entry)
    {
        var kindMatches = SelectedFilter switch
        {
            LogFilter.Commands => entry.Kind is LogEntryKind.Command,
            LogFilter.Output   => entry.Kind is LogEntryKind.StdOut or LogEntryKind.Info,
            LogFilter.Warnings => entry.Kind is LogEntryKind.StdErr,
            LogFilter.Errors   => entry.Kind is LogEntryKind.Error,
            _                  => true,
        };

        if (!kindMatches)
            return false;

        return string.IsNullOrWhiteSpace(SearchText)
            || entry.Text.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }
}
