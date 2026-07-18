namespace AppiumSetupManager.Localization;

public static class Strings
{
    public const string AppTitle            = "Appium Setup Manager";
    public const string NavDashboard        = "Dashboard";
    public const string NavInstall          = "Installation";
    public const string NavDoctor           = "Doctor";
    public const string NavStorage          = "Storage";
    public const string NavSettings         = "Settings";

    // Visual Redesign R1 — new nav destination labels (sidebar row text).
    public const string NavEnvironment      = "Environment";
    public const string NavUpdates          = "Updates";
    public const string NavHistory          = "History";
    public const string NavLogs             = "Logs";

    // Shell / branding (Organic sage restyle)
    public const string BrandBadge           = "A";

    public const string TopBarSearchPlaceholder = "Search components, logs, docs…";
    public const string TopBarSearch            = "Search";
    public const string TopBarToggleTheme       = "Toggle color theme";
    public const string TopBarSettings          = "Settings";

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
    public const string DashboardHealthIssuesFormat = "{0} configuration issue(s) detected";
    public const string DashboardHealthAllClear     = "All environment paths verified";
    public const string DashboardNotDetected = "Not detected";

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

    // ── Visual Redesign R1 — Dashboard reskin ──────────────────────────────────
    public const string DashboardHeroKicker        = "Automation Environment Status";
    public const string DashboardHeroHeadlineFormat = "{0:0}% environment ready";
    public const string DashboardHeroEstimate      = "Est. 1–2 min";
    public const string DashboardQuickInstall      = "Quick Install";
    public const string DashboardAdvancedInstall   = "Advanced Install";
    public const string DashboardEnvVarsSummaryTitle = "Environment Variables";
    public const string DashboardEnvVarStatusConfigured = "Configured";
    public const string DashboardEnvVarStatusMissing    = "Missing";
    public const string DashboardComponentsSectionTitle = "Components";
    public const string DashboardOpenFolder        = "Open folder";
    public const string DashboardRepair            = "Repair";
    public const string DashboardRequiredVersionFormat = "req {0}+";

    // ── Visual Redesign R1 — Installation flow-graph reskin ────────────────────
    public const string InstallCompletedGroup = "Completed";
    public const string InstallActiveGroup    = "In Progress";
    public const string InstallQueuedGroup    = "Queued";
    public const string InstallFailedGroup    = "Failed";
    public const string InstallPause          = "Pause";
    public const string InstallStepDone       = "Done";
    public const string InstallStepSkipped    = "Skipped";

    // ── Visual Redesign R1 — Doctor reskin ──────────────────────────────────────
    public const string DoctorHeroKicker           = "Overall Health Score";
    public const string DoctorHeroHeadlineFormat    = "{0}% healthy";
    public const string DoctorHeroAllClear          = "Every check is passing.";
    public const string DoctorHeroIssuesFormat      = "{0} check(s) need attention";
    public const string DoctorRootCausePrefix       = "Root cause:";
    public const string DoctorSuggestedFixPrefix    = "Suggested fix:";
    public const string DoctorSuggestedFixCommandFormat = "Run `{0}`";
    public const string DoctorSeverityCritical      = "Critical";
    public const string DoctorSeverityWarning       = "Warning";
    public const string DoctorSeverityFixed         = "Fixed";

    // ── Visual Redesign R1 — Storage screen ─────────────────────────────────────
    public const string StorageHeroKicker        = "Storage Reclaimable";
    public const string StorageHeroHeadlineFormat = "{0:0.0} GB reclaimable";
    public const string StorageHeroAllClearFormat = "{0} item(s) scanned across {1} categories.";
    public const string StorageEmptyStateTitle   = "No storage data yet";
    public const string StorageEmptyStateBody    = "Run a scan to find cached files, old SDK images, and build artifacts you can safely remove.";
    public const string StorageScan              = "Scan";
    public const string StoragePreview           = "Preview Cleanup";
    public const string StorageConfirmCleanup    = "Delete Selected";
    public const string StorageCancelPreview     = "Cancel";
    public const string StorageSelectedCountFormat = "{0} selected";
    public const string StorageReclaimFormat     = "{0:0.0} GB";
    public const string StorageSizeGbFormat      = "{0:0.0} GB";
    public const string StorageRiskLow           = "Low risk";
    public const string StorageRiskMedium        = "Medium risk";
    public const string StoragePreviewTitle      = "Confirm Cleanup";
    public const string StoragePreviewBody       = "The following items will be permanently deleted. This cannot be undone.";
    public const string StorageToastFormat       = "Freed {0:0.0} GB";
    public const string StorageDismiss           = "Dismiss";
    public const string StorageCleaning          = "Cleaning…";

    // ── Visual Redesign R1 — Environment screen ─────────────────────────────────
    public const string EnvironmentVariablesSectionTitle = "Environment Variables";
    public const string EnvironmentToolchainSectionTitle = "Toolchain Locations";
    public const string EnvironmentBackupsSectionTitle   = "Backups & Restore Points";
    public const string EnvironmentSaveChanges           = "Save Changes";
    public const string EnvironmentResetFormat           = "Reset {0} to original value";
    public const string EnvironmentRestore               = "Restore";
    public const string EnvironmentRestoreFormat         = "Restore {0}";
    public const string EnvironmentBackupsEmptyState     = "No backups yet — edits to environment variables will appear here.";
    public const string EnvironmentToolchainNode         = "Node.js";
    public const string EnvironmentToolchainNpm          = "npm";
    public const string EnvironmentToolchainJava         = "Java";
    public const string EnvironmentToolchainAdb          = "ADB";
    public const string EnvironmentToolchainSdk          = "SDK Location";
    public const string EnvironmentNoSingleVersion       = "—";
    public const string EnvironmentManualEditReason      = "Manual edit from Environment screen";
    public const string EnvironmentRestoreReason         = "Reverted via Restore";
    public const string EnvironmentJustNow               = "Just now";
    public const string EnvironmentMinutesAgoFormat      = "{0}m ago";
    public const string EnvironmentHoursAgoFormat        = "{0}h ago";
    public const string EnvironmentDaysAgoFormat         = "{0}d ago";
    public const string EnvironmentTimeReasonFormat      = "{0} · {1}";

    // ── Visual Redesign R1 step 4 — Updates screen ──────────────────────────────
    public const string UpdatesTitle                    = "Updates";
    public const string UpdatesStatusUpToDate           = "Up to date";
    public const string UpdatesStatusAvailable          = "Update available";
    public const string UpdatesSubtitleAvailableFormat  = "{0} update(s) available across your toolchain";
    public const string UpdatesSubtitleAllUpToDate      = "You're all set — everything is up to date";
    public const string UpdatesReleaseNoteLiveFormat     = "Update available: {0} → {1}";
    public const string UpdatesReleaseNoteFloorFormat    = "Update available: {0} → v{1}+ required";
    public const string UpdatesUnknownVersion            = "unknown";
    public const string UpdatesUpdateAll                 = "Update All";
    public const string UpdatesInstalledVersionFormat    = "Installed {0}";
    public const string UpdatesLatestVersionFormat       = "Latest {0}";
    public const string UpdatesUpdateComponentFormat     = "Update {0}";
    public const string UpdatesEmptyTitle                = "You're all set";
    public const string UpdatesEmptyBody                 = "Everything in your toolchain is already up to date.";

    // ── Visual Redesign R1 step 5 — History screen ──────────────────────────────
    public const string HistoryTitle           = "History";
    public const string HistorySubtitle        = "A timeline of installs, updates, repairs and changes";
    public const string HistoryTypeInstall     = "Install";
    public const string HistoryTypeUpdate      = "Update";
    public const string HistoryTypeRepair      = "Repair";
    public const string HistoryTypeCleanup     = "Cleanup";
    public const string HistoryTypeSetup       = "Setup";
    public const string HistoryDayToday        = "Today";
    public const string HistoryDayYesterday    = "Yesterday";
    public const string HistoryRollback        = "Rollback";
    public const string HistoryRollbackFormat  = "Roll back {0}";
    public const string HistoryEmptyTitle      = "No activity yet";
    public const string HistoryEmptyBody       = "Installs, updates, repairs and changes made by this app will show up here.";

    // ── Visual Redesign R1 step 6 — Logs screen ─────────────────────────────────
    public const string LogsTitle             = "Logs";
    public const string LogsSubtitle          = "Full command history with search and filters";
    public const string LogsSearchPlaceholder = "Search logs…";
    public const string LogsSearchName        = "Search logs";
    public const string LogsFilterAll         = "All";
    public const string LogsFilterCommands    = "Commands";
    public const string LogsFilterOutput      = "Output";
    public const string LogsFilterWarnings    = "Warnings";
    public const string LogsFilterErrors      = "Errors";
    public const string LogsEmptyState        = "No matching log entries.";

    // ── Visual Redesign R1 step 6 — Settings screen ─────────────────────────────
    public const string SettingsTitle    = "Settings";
    public const string SettingsSubtitle = "Notifications, update checks and keyboard shortcuts";

    // Card 1 — Notifications & Updates (four persisted toggles)
    public const string SettingsCardNotifications = "Notifications & Updates";
    public const string SettingsAutoUpdateChecksLabel       = "Automatic Update Checks";
    public const string SettingsAutoUpdateChecksDescription = "Check the npm registry for newer versions when the Updates screen loads.";
    public const string SettingsNotifyFailureLabel          = "Notify on Failure";
    public const string SettingsNotifyFailureDescription    = "Show a notification when an install or repair fails.";
    public const string SettingsNotifyCompletionLabel       = "Notify on Completion";
    public const string SettingsNotifyCompletionDescription = "Show a notification when an install or update finishes successfully.";
    public const string SettingsUsageDataLabel              = "Anonymous Usage Data";
    public const string SettingsUsageDataDescription        = "Share anonymized usage statistics to help improve the app.";

    // Card 2 — Keyboard Shortcuts
    public const string SettingsCardShortcuts = "Keyboard Shortcuts";
    // Shortcut row labels (gestures: Cmd/Ctrl+Shift+I, Cmd/Ctrl+`, Cmd/Ctrl+Shift+D,
    // Cmd/Ctrl+K, Cmd/Ctrl+Shift+L — bound in MainWindow.axaml.cs).
    public const string SettingsShortcutQuickInstall = "Quick Install";
    public const string SettingsShortcutOpenConsole  = "Open Command Console";
    public const string SettingsShortcutRunDoctor    = "Run Doctor";
    public const string SettingsShortcutFocusSearch  = "Focus Search";
    public const string SettingsShortcutToggleTheme  = "Toggle Theme";
    // Key badges — macOS symbol style.
    public const string SettingsBadgeMacQuickInstall = "⌘⇧I";
    public const string SettingsBadgeMacOpenConsole  = "⌘`";
    public const string SettingsBadgeMacRunDoctor    = "⌘⇧D";
    public const string SettingsBadgeMacFocusSearch  = "⌘K";
    public const string SettingsBadgeMacToggleTheme  = "⌘⇧L";
    // Key badges — Windows/Linux textual style.
    public const string SettingsBadgeWinQuickInstall = "Ctrl+Shift+I";
    public const string SettingsBadgeWinOpenConsole  = "Ctrl+`";
    public const string SettingsBadgeWinRunDoctor    = "Ctrl+Shift+D";
    public const string SettingsBadgeWinFocusSearch  = "Ctrl+K";
    public const string SettingsBadgeWinToggleTheme  = "Ctrl+Shift+L";
}
