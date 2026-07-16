namespace AppiumSetupManager.Localization;

public static class Strings
{
    public const string AppTitle            = "Appium Setup Manager";
    public const string NavDashboard        = "Dashboard";
    public const string NavInstall          = "Install";
    public const string NavDoctor           = "Doctor";
    public const string NavStorage          = "Storage";

    // Shell / branding (Midnight Slate restyle)
    public const string BrandName            = "Appium";
    public const string BrandSubtitle        = "SETUP MANAGER";
    public const string BrandBadge           = "A";
    public const string TopBarTitle          = "Dependency Orchestrator";
    public const string TopBarClusterStatus  = "Cluster Status";
    public const string TopBarActiveNodes    = "Active Nodes";
    public const string TopBarSearchPlaceholder = "Search resources…";
    public const string NavSettings          = "Settings";
    public const string NavSupport           = "Support";
    public const string NavDashboardGlyph    = "▦";
    public const string NavInstallGlyph      = "↧";
    public const string NavDoctorGlyph       = "✚";
    public const string NavStorageGlyph      = "▤";
    public const string NavSettingsGlyph     = "⚙";
    public const string NavSupportGlyph      = "?";

    // Status pill labels
    public const string PillInstalled        = "INSTALLED";
    public const string PillOutdated         = "OUTDATED";
    public const string PillMissing          = "MISSING";

    public const string DashboardTitle       = "System Environment Configuration";
    public const string DashboardSubtitle    = "Verify and configure global paths for Android, iOS, and Web automation runtimes.";
    public const string DashboardScanSystem  = "Scan System";
    public const string DashboardPlatformPrimary  = "Primary";
    public const string DashboardPlatformDetected = "DETECTED";
    public const string DashboardArchitecture     = "Architecture:";
    public const string DashboardKernel           = "Kernel:";
    public const string DashboardXcode            = "Xcode:";
    public const string DashboardGlobalEnvVars    = "Global Environment Variables";
    public const string DashboardSystemPaths      = "System Paths";
    public const string DashboardAppiumBinary     = "Appium Binary";
    public const string DashboardAdbExecutable    = "ADB Executable";
    public const string DashboardNodeRuntime      = "Node.js Runtime";
    public const string DashboardEnvironmentHealth = "ENVIRONMENT HEALTH";
    public const string DashboardHealthIssuesFormat = "{0} configuration issue(s) detected";
    public const string DashboardHealthAllClear     = "All environment paths verified";
    public const string DashboardEnvVerified = "VERIFIED";
    public const string DashboardEnvMissing  = "MISSING";
    public const string DashboardEnvWarning  = "WARNING";
    public const string DashboardNotDetected = "Not detected";
    public const string DashboardDetectAndSet = "Detect & Set";
    public const string DashboardNoExistingInstallFound = "No existing installation found at the default location — try Install instead.";

    public const string CommandLogTitle     = "Command Log";
    public const string CommandLogFileName  = "appium-orchestrator.log";
    public const string CommandLogCollapse  = "▲";
    public const string CommandLogExpand    = "▼";
    public const string CommandLogExport    = "Export";
    public const string DashboardInstall       = "Install";
    public const string DashboardUpdate        = "Update";
    public const string DashboardInstalling    = "Installing…";
    public const string DashboardScanning     = "Scanning...";
    public const string DashboardScanCancelled = "Scan cancelled";
    public const string DashboardScanFailed    = "Scan failed";
    public const string DashboardSummaryFormat = "{0} of {1} components ready";
    public const string DashboardColComponent  = "Component";
    public const string DashboardColVersion    = "Version";
    public const string DashboardColPath       = "Path";
    public const string StatusFound          = "Found";
    public const string StatusOutdated       = "Outdated";
    public const string StatusNotFound       = "Not installed";
    public const string StatusNotApplicable  = "N/A";
    public const string InstallPlaceholder    = "Install — coming in Phase 3";
    public const string DoctorPlaceholder     = "Doctor — coming in Phase 4";
    public const string StoragePlaceholder    = "Storage — coming in Phase 5";
    public const string InstallAllMissing     = "Install All Missing";
    public const string InstallStart          = "Install";
    public const string InstallCancel         = "Cancel";
    public const string InstallRetry          = "Retry";
    public const string InstallExpandCommand  = "▼";
    public const string InstallCollapseCommand = "▲";
    public const string InstallSummaryFormat  = "{0} of {1} steps succeeded";
    public const string InstallCancelled      = "Installation cancelled";
    public const string InstallFailed         = "Installation failed";
    public const string InstallSuggestedFix   = "Suggested fix:";
    public const string InstallLastRunLog     = "Last run log";
    public const string DoctorRerun             = "Re-run";
    public const string DoctorFix               = "Fix";
    public const string DoctorGroupDependencies = "Dependencies";
    public const string DoctorGroupEnvironment  = "Environment";
    public const string DoctorGroupDrivers      = "Drivers";
    public const string DoctorGroupTools        = "Tools";
    public const string DoctorSummaryFormat     = "{0} of {1} checks passed";
    public const string DoctorRunning           = "Running checks…";
    public const string DoctorFixing            = "Fixing…";
}
