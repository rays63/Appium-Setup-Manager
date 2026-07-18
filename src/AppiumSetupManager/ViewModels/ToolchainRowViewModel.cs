namespace AppiumSetupManager.ViewModels;

/// <summary>
/// A single read-only row in the Environment screen's "Toolchain Locations" list — plain display
/// data with no commands, built fresh from the latest detection scan each time the screen reloads.
/// </summary>
public sealed class ToolchainRowViewModel
{
    public string Label { get; }
    public string PathDisplay { get; }
    public string VersionDisplay { get; }

    public ToolchainRowViewModel(string label, string pathDisplay, string versionDisplay)
    {
        Label = label;
        PathDisplay = pathDisplay;
        VersionDisplay = versionDisplay;
    }
}
