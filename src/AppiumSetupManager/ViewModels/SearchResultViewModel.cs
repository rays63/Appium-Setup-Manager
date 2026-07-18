using CommunityToolkit.Mvvm.Input;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// One row in the top-bar quick-search popup: a label, an optional muted detail line, and the
/// action to run when the row is chosen. Immutable — the result list is rebuilt on every
/// query change, never mutated in place.
/// </summary>
public sealed class SearchResultViewModel
{
    public string  Label  { get; }
    public string? Detail { get; }

    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    /// <summary>False only for the placeholder "No results" row, which must do nothing.</summary>
    public bool IsExecutable { get; }

    public IRelayCommand ExecuteCommand { get; }

    public SearchResultViewModel(string label, string? detail, Action? execute)
    {
        Label        = label;
        Detail       = detail;
        IsExecutable = execute is not null;

        ExecuteCommand = new RelayCommand(() => execute?.Invoke());
    }
}
