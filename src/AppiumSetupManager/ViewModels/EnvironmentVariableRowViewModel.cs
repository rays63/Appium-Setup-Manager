using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppiumSetupManager.ViewModels;

/// <summary>
/// One hand-editable environment variable row on the Environment screen (JAVA_HOME/ANDROID_HOME
/// only — PATH is deliberately excluded, see EnvironmentViewModel). Tracks the value currently
/// persisted (<see cref="OriginalValue"/>) against whatever the user has typed
/// (<see cref="EditedValue"/>) so the host view model knows which rows need saving.
/// </summary>
public partial class EnvironmentVariableRowViewModel : ObservableObject
{
    /// <summary>The environment variable name, e.g. "JAVA_HOME".</summary>
    public string Name { get; }

    /// <summary>The value as last loaded from the environment — never mutated after construction.</summary>
    public string OriginalValue { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _editedValue;

    /// <summary>True when the user has typed something different from <see cref="OriginalValue"/>.</summary>
    public bool IsDirty => EditedValue != OriginalValue;

    public EnvironmentVariableRowViewModel(string name, string originalValue)
    {
        Name = name;
        OriginalValue = originalValue;
        _editedValue = originalValue;
    }

    [RelayCommand]
    private void Reset() => EditedValue = OriginalValue;
}
