namespace AppiumSetupManager.Core.Services;

public sealed record CatalogEntry(
    string ComponentName,
    string? MacCommand,
    string? WindowsCommand,
    string? LinuxCommand,
    bool MacOnly,
    string? PostInstallEnvVar,
    string? PostInstallPathDir,
    string? SuggestedFix
);

public static class InstallCatalog
{
    public static readonly IReadOnlyList<CatalogEntry> All = new List<CatalogEntry>
    {
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
            SuggestedFix:       "npm is installed with Node.js — reinstall Node.js to fix"
        ),

        new CatalogEntry(
            ComponentName:      "JDK 21",
            MacCommand:         "brew install --cask temurin@21",
            WindowsCommand:     "winget install EclipseAdoptium.Temurin.21.JDK",
            LinuxCommand:       "sudo apt-get install -y temurin-21-jdk",
            MacOnly:            false,
            PostInstallEnvVar:  null,
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
            SuggestedFix:       "Ensure Node.js and npm are installed, then retry"
        ),

        new CatalogEntry(
            ComponentName:      "UiAutomator2 Driver",
            MacCommand:         "appium driver install uiautomator2",
            WindowsCommand:     "appium driver install uiautomator2",
            LinuxCommand:       "appium driver install uiautomator2",
            MacOnly:            false,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Ensure Appium server is installed first"
        ),

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
            ComponentName:      "XCUITest Driver",
            MacCommand:         "appium driver install xcuitest",
            WindowsCommand:     null,
            LinuxCommand:       null,
            MacOnly:            true,
            PostInstallEnvVar:  null,
            PostInstallPathDir: null,
            SuggestedFix:       "Ensure Appium server is installed and Xcode CLI Tools are present"
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
