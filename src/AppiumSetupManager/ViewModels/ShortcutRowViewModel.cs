namespace AppiumSetupManager.ViewModels;

/// <summary>
/// One row in the Settings screen's keyboard-shortcuts list: a human-readable action label and
/// its platform-appropriate key badge ("⌘⇧I" on macOS, "Ctrl+Shift+I" elsewhere).
/// </summary>
public sealed record ShortcutRowViewModel(string Label, string KeyBadge);
