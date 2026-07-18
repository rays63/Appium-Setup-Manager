namespace AppiumSetupManager.Core.Services;

public sealed record CatalogEntry(
    string ComponentName,
    string? MacCommand,
    string? WindowsCommand,
    string? LinuxCommand,
    bool MacOnly,
    string? PostInstallEnvVar,
    string? PostInstallPathDir,
    string? SuggestedFix,
    string? NpmPackageName = null
);

public static class InstallCatalog
{
    // Some catalog entries use a user-facing name that differs from the DetectionService probe
    // that actually verifies they're present — "JDK 21" is satisfied once the "JDK" probe reports
    // Found; "Android SDK" is satisfied once "ADB" does. Without this alias, name-matching against
    // a scan can never match these two entries (e.g. GetSkipSet in InstallerService, or the Updates
    // screen resolving a catalog entry to its ComponentStatus), so they'd be treated as never
    // installed/never updateable even when already present.
    public static readonly Dictionary<string, string> DetectionNameAlias = new(StringComparer.Ordinal)
    {
        ["JDK 21"]      = "JDK",
        ["Android SDK"] = "ADB",
    };

    public static readonly IReadOnlyList<CatalogEntry> All = new List<CatalogEntry>
    {
        // macOS-only prerequisites, in dependency order: Xcode CLI Tools must exist before
        // Homebrew's own installer runs, and Homebrew must exist before anything below that
        // installs via `brew`.
        new CatalogEntry(
            ComponentName:      "Xcode CLI Tools",
            MacCommand:         "xcode-select --install",
            WindowsCommand:     null,
            LinuxCommand:       null,
            MacOnly:            true,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Run 'xcode-select --install' manually in Terminal"
        ),

        new CatalogEntry(
            ComponentName:      "Homebrew",
            MacCommand:         "/bin/bash -c \"$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)\"",
            WindowsCommand:     null,
            LinuxCommand:       null,
            MacOnly:            true,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Run the official installer yourself in Terminal: https://brew.sh — needed if the automated install requires a password (sudo) prompt this app can't answer"
        ),

        new CatalogEntry(
            ComponentName:      "Node.js",
            MacCommand:         "brew install node",
            WindowsCommand:     "winget install OpenJS.NodeJS.LTS",
            LinuxCommand:       "sudo apt-get install -y nodejs",
            MacOnly:            false,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Ensure Homebrew (macOS), winget (Windows), or apt (Linux) is available and try again"
        ),

        new CatalogEntry(
            ComponentName:      "npm",
            MacCommand:         string.Empty,
            WindowsCommand:     string.Empty,
            LinuxCommand:       string.Empty,
            MacOnly:            false,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "npm is installed with Node.js — reinstall Node.js to fix",
            NpmPackageName:     "npm"
        ),

        new CatalogEntry(
            ComponentName:      "JDK 21",
            MacCommand:         "brew install --cask temurin@21",
            WindowsCommand:     "winget install EclipseAdoptium.Temurin.21.JDK",
            LinuxCommand:       "sudo apt-get install -y temurin-21-jdk",
            MacOnly:            false,
            PostInstallEnvVar:  "JAVA_HOME",
            PostInstallPathDir: null,
            SuggestedFix:       "Ensure Homebrew (macOS), winget (Windows), or apt with Adoptium repository (Linux) is available"
        ),

        new CatalogEntry(
            ComponentName:      "Android SDK",
            MacCommand:         "brew install --cask android-commandlinetools",
            WindowsCommand:     "winget install Google.AndroidStudio",
            LinuxCommand:       "sudo apt-get install -y android-sdk",
            MacOnly:            false,
            PostInstallEnvVar:  "ANDROID_HOME",
            PostInstallPathDir: "<platform-tools>",
            SuggestedFix:       "Ensure Homebrew (macOS), winget (Windows), or apt (Linux) is available"
        ),

        new CatalogEntry(
            ComponentName:      "Appium",
            MacCommand:         "npm install -g appium",
            WindowsCommand:     "npm install -g appium",
            LinuxCommand:       "npm install -g appium",
            MacOnly:            false,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Ensure Node.js and npm are installed, then retry",
            NpmPackageName:     "appium"
        ),

        new CatalogEntry(
            ComponentName:      "UiAutomator2 Driver",
            MacCommand:         "appium driver install uiautomator2",
            WindowsCommand:     "appium driver install uiautomator2",
            LinuxCommand:       "appium driver install uiautomator2",
            MacOnly:            false,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Ensure Appium server is installed first",
            NpmPackageName:     "appium-uiautomator2-driver"
        ),

        new CatalogEntry(
            ComponentName:      "XCUITest Driver",
            MacCommand:         "appium driver install xcuitest",
            WindowsCommand:     null,
            LinuxCommand:       null,
            MacOnly:            true,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Ensure Appium server is installed and Xcode CLI Tools are present",
            NpmPackageName:     "appium-xcuitest-driver"
        ),

        new CatalogEntry(
            ComponentName:      "Appium Inspector",
            MacCommand:         "brew install --cask appium-inspector",
            WindowsCommand:     null,
            LinuxCommand:       null,
            MacOnly:            true,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Download Appium Inspector from https://github.com/appium/appium-inspector/releases"
        ),
    };
}
