using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppiumSetupManager.Core.Models;

namespace AppiumSetupManager.ViewModels;

public partial class InstallStepViewModel : ObservableObject
{
    // Immutable key used by InstallViewModel._stepMap
    public string ComponentName { get; }

    // Set by InstallViewModel at creation — avoids ancestor binding in AXAML.
    public ICommand? RetryCommand { get; }

    [ObservableProperty]
    private InstallStepState _state;

    [ObservableProperty]
    private string _command = string.Empty;

    [ObservableProperty]
    private bool _isCommandExpanded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _suggestedFix;

    // True only for steps that apply to this platform (set false for iOS steps on Windows/Linux).
    // The service never emits these steps at all, but InstallViewModel may pre-populate VMs.
    // Default true — hidden steps are created with IsVisible=false.
    public bool IsVisible { get; }

    // Computed — drives error/retry row visibility. Must raise PropertyChanged when State changes.
    public bool IsFailure => State == InstallStepState.Failed;

    public InstallStepViewModel(InstallStep step, ICommand? retryCommand = null, bool isVisible = true)
    {
        ComponentName = step.ComponentName;
        _state        = step.State;
        _command      = step.Command;
        _errorMessage = step.ErrorMessage;
        _suggestedFix = step.SuggestedFix;
        IsVisible     = isVisible;
        RetryCommand  = retryCommand;
    }

    // Called when the service emits a state-transition update for this component.
    public void ApplyUpdate(InstallStep step)
    {
        State         = step.State;
        Command       = step.Command;
        ErrorMessage  = step.ErrorMessage;
        SuggestedFix  = step.SuggestedFix;
    }

    [RelayCommand]
    private void ToggleCommandExpanded() => IsCommandExpanded = !IsCommandExpanded;

    // Raise IsFailure notification whenever State changes.
    partial void OnStateChanged(InstallStepState value) =>
        OnPropertyChanged(nameof(IsFailure));
}
