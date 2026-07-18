# Appium Setup Manager — Project Documents

*Derived from PRD v1.0 · July 14, 2026*

---

## Table of Contents

1. [Project Summary](#1-project-summary)
2. [Architecture Design Document](#2-architecture-design-document)
3. [Technical Stack Decision](#3-technical-stack-decision)
4. [Feature Specification](#4-feature-specification)
5. [UI/UX Flow & Screen Map](#5-uiux-flow--screen-map)
6. [Component Detection Specification](#6-component-detection-specification)
7. [Installation Sequence Specification](#7-installation-sequence-specification)
8. [Storage & Cleanup Specification](#8-storage--cleanup-specification)
9. [Development Phases & Milestones](#9-development-phases--milestones)
10. [Test Strategy](#10-test-strategy)
11. [Risk Register](#11-risk-register)
12. [Open Questions Tracker](#12-open-questions-tracker)

---

## 1. Project Summary

| Field | Value |
|---|---|
| Product | Appium Setup Manager |
| Type | Cross-platform desktop GUI application |
| Purpose | Automated Appium environment setup, diagnostics, and cleanup |
| Platforms | Windows 10+, macOS 12+, Ubuntu / Fedora |
| Tech Stack | Avalonia UI (.NET / C#) — recommended |
| Version | 1.0 |
| Status | Pre-development |

### Problem Being Solved

QA engineers must manually install and configure 8+ interdependent tools (Node.js, JDK, Android SDK, Xcode CLT, Appium server, drivers, Inspector) with OS-specific environment variables. This is slow, inconsistent, and a top source of onboarding friction. Android/iOS emulators and caches silently consume tens of gigabytes with no easy audit path.

### What the App Does

- Detects all Appium-related tools already installed and their versions on launch.
- Installs missing or outdated components via CLI commands with one button press.
- Runs health diagnostics (appium-doctor equivalent) with visual pass/warn/fail results.
- Provides a storage audit view with safe, CLI-backed cleanup of caches, emulators, and SDK junk.
- Streams a live command log so every action is transparent and exportable.

---

## 2. Architecture Design Document

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Avalonia UI Layer                      │
│  Dashboard | Install | Doctor | Storage | Log               │
└────────────────────────┬────────────────────────────────────┘
                         │  ViewModel (MVVM)
┌────────────────────────▼────────────────────────────────────┐
│                    Application Services                      │
│  DetectionService | InstallerService | DoctorService        │
│  StorageService   | CleanupService   | LogService           │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                  Process Orchestration Layer                  │
│  CommandRunner (async stdout/stderr streaming)               │
│  EnvironmentVariableManager (per-OS PATH/env editing)        │
│  ElevationManager (UAC / sudo prompts)                       │
└────────────────────────┬────────────────────────────────────┘
                         │  System.Diagnostics.Process
┌────────────────────────▼────────────────────────────────────┐
│                     OS / CLI Layer                           │
│  node, npm, java, adb, sdkmanager, appium, xcodebuild,      │
│  simctl, avdmanager, brew, winget, apt                       │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Project Structure

```
AppiumSetupManager/
├── AppiumSetupManager.sln
├── src/
│   ├── AppiumSetupManager/               # Main app project
│   │   ├── App.axaml / App.axaml.cs
│   │   ├── Assets/
│   │   ├── Views/
│   │   │   ├── MainWindow.axaml
│   │   │   ├── DashboardView.axaml
│   │   │   ├── InstallView.axaml
│   │   │   ├── DoctorView.axaml
│   │   │   ├── StorageView.axaml
│   │   │   └── CommandLogView.axaml
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── DashboardViewModel.cs
│   │   │   ├── InstallViewModel.cs
│   │   │   ├── DoctorViewModel.cs
│   │   │   ├── StorageViewModel.cs
│   │   │   └── CommandLogViewModel.cs
│   │   └── Program.cs
│   ├── AppiumSetupManager.Core/          # Business logic (no UI dependency)
│   │   ├── Models/
│   │   │   ├── ComponentStatus.cs
│   │   │   ├── InstallStep.cs
│   │   │   ├── DoctorCheck.cs
│   │   │   ├── StorageItem.cs
│   │   │   └── CommandResult.cs
│   │   ├── Services/
│   │   │   ├── DetectionService.cs
│   │   │   ├── InstallerService.cs
│   │   │   ├── DoctorService.cs
│   │   │   ├── StorageService.cs
│   │   │   ├── CleanupService.cs
│   │   │   └── LogService.cs
│   │   ├── Infrastructure/
│   │   │   ├── CommandRunner.cs
│   │   │   ├── EnvironmentVariableManager.cs
│   │   │   └── ElevationManager.cs
│   │   └── Platform/
│   │       ├── IPlatformAdapter.cs
│   │       ├── WindowsAdapter.cs
│   │       ├── MacOsAdapter.cs
│   │       └── LinuxAdapter.cs
│   └── AppiumSetupManager.Tests/
│       ├── Services/
│       ├── Infrastructure/
│       └── Platform/
├── docs/
└── build/
```

### 2.3 Key Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| UI pattern | MVVM | Standard for Avalonia; clean separation of UI and logic |
| Async model | `async/await` + `IProgress<T>` | Non-blocking CLI execution; real-time progress streaming |
| Platform isolation | `IPlatformAdapter` per OS | Swap OS-specific logic without touching services |
| Core/UI split | Separate `Core` project | Allows unit testing all logic without UI instantiation |
| Command execution | `System.Diagnostics.Process` async streaming | Mature, single-language, no external dependency |
| State management | Reactive properties (CommunityToolkit.Mvvm) | Minimal boilerplate, INotifyPropertyChanged auto-generation |

---

## 3. Technical Stack Decision

### Chosen Stack: Avalonia UI (.NET 8 / C#)

| Layer | Technology |
|---|---|
| UI framework | Avalonia UI 11.x |
| Language | C# 12 / .NET 8 |
| MVVM toolkit | CommunityToolkit.Mvvm |
| Process execution | System.Diagnostics.Process |
| Async | Task / IAsyncEnumerable for streaming |
| Logging | Microsoft.Extensions.Logging + Serilog |
| Unit testing | xUnit + FluentAssertions |
| Installer packaging | Velopack (cross-platform) |

### .NET 8 Target Runtimes

| Platform | RID | Notes |
|---|---|---|
| Windows | `win-x64` | Self-contained exe with NativeAOT option |
| macOS (Apple Silicon) | `osx-arm64` | Requires notarization for distribution |
| macOS (Intel) | `osx-x64` | |
| Linux | `linux-x64` | AppImage or .deb / .rpm |

### Dependencies

```xml
<!-- Core -->
<PackageReference Include="Avalonia" Version="11.*" />
<PackageReference Include="Avalonia.Desktop" Version="11.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />

<!-- Logging -->
<PackageReference Include="Serilog" Version="3.*" />
<PackageReference Include="Serilog.Sinks.File" Version="5.*" />

<!-- Packaging -->
<PackageReference Include="Velopack" Version="0.*" />

<!-- Testing -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
<PackageReference Include="NSubstitute" Version="5.*" />
```

---

## 4. Feature Specification

### 4.1 Feature: Environment Detection

**Trigger:** App launch (automatic) + manual "Re-scan" button.

**Behavior:**
- Runs all probes concurrently (parallel `Task.WhenAll`).
- Each probe: spawn CLI command, parse stdout/stderr, extract version string via regex.
- Results bound to `DashboardViewModel` as a list of `ComponentStatus`.
- Scan must complete in < 10 seconds on a typical machine.

**ComponentStatus model:**

```csharp
public record ComponentStatus(
    string Name,
    DetectionState State,       // NotFound | Found | Outdated
    string? InstalledVersion,
    string? RequiredVersion,
    string? InstallPath,
    string? EnvVar
);
```

**States displayed:**
- `Found` → green checkmark + version
- `Outdated` → amber warning + installed vs required version
- `NotFound` → red X + "Not installed"

---

### 4.2 Feature: Installation

**Two modes:**

| Mode | Description |
|---|---|
| Quick Preset | Installs all missing/outdated components with defaults, no choices required |
| Advanced | Checklist of components; user selects which to install and optionally pins a version |

**InstallStep model:**

```csharp
public record InstallStep(
    string ComponentName,
    string Command,
    InstallStepState State,     // Pending | Running | Done | Failed | Skipped
    string? ErrorMessage,
    string? SuggestedFix
);
```

**Requirements:**
- Skip steps where component is already at the required version (idempotent).
- Auto-configure `ANDROID_HOME`, `JAVA_HOME`, and add entries to `PATH` per OS after install.
- On failure: show exact command + stderr + suggested fix + "Retry" button.
- All commands streamed to the Command Log in real time.

**Install order (dependency-aware):**

```
1. Node.js / npm
2. JDK
3. Android SDK / platform-tools / ADB
4. Xcode CLT (macOS only)
5. Appium server  (npm install -g appium)
6. UiAutomator2 driver  (appium driver install uiautomator2)
7. XCUITest driver  (appium driver install xcuitest — macOS only)
8. Appium Inspector  (platform-specific installer or brew cask)
9. Environment variable verification pass
```

---

### 4.3 Feature: Health Check (Doctor)

**DoctorCheck model:**

```csharp
public record DoctorCheck(
    string Name,
    CheckResult Result,         // Pass | Warn | Fail
    string Description,
    string? RemediationCommand,
    bool CanAutoFix
);
```

**Checks to run:**

| Check | Pass Condition |
|---|---|
| Node.js present + version | >= minimum version |
| npm present | present |
| JDK present + JAVA_HOME set | version >= 11, env var points to JDK root |
| ANDROID_HOME set | env var set and path exists |
| ADB on PATH | `adb version` exits 0 |
| Appium server present | `appium -v` exits 0 |
| UiAutomator2 driver installed | listed in `appium driver list --installed` |
| XCUITest driver installed (macOS) | listed in `appium driver list --installed` |
| Appium Inspector present | install path or app bundle found |

**Auto-fix:** where `CanAutoFix = true`, a "Fix" button triggers the remediation command and re-runs the check.

---

### 4.4 Feature: Storage View & Cleanup

**StorageItem model:**

```csharp
public record StorageItem(
    string Category,            // "Emulators" | "Simulators" | "Caches" | "SDKJunk"
    string Name,
    string Path,
    long SizeBytes,
    bool IsSafeToDelete,
    string DeleteCommand
);
```

**Categories and cleanup commands:**

| Category | Discovery | Cleanup Command |
|---|---|---|
| Android AVDs | `avdmanager list avd` | `avdmanager delete avd -n <name>` |
| iOS Simulator runtimes | `xcrun simctl list runtimes` | `xcrun simctl delete <udid>` |
| npm cache | `npm cache verify` for size | `npm cache clean --force` |
| Gradle caches | `~/.gradle/caches` directory size | Delete directory via `rm -rf` (with confirmation) |
| Derived Data / Xcode | `~/Library/Developer/Xcode/DerivedData` | Delete directory |
| Stale Appium drivers | `appium driver list` for unrecognized entries | `appium driver uninstall <name>` |

**Safety rules:**
- Nothing deleted without a confirmation dialog showing item name, path, and size freed.
- Dry-run preview lists all items before any action.
- Distinguish "safe to delete" (caches, stale runtimes) from "user may want this" (named AVDs).

---

### 4.5 Feature: Command Log

- Persistent panel at the bottom of every view, collapsible.
- Appends every command issued: timestamp + command string + stdout/stderr.
- Color coding: command line in blue, stdout in default, stderr in amber, errors in red.
- "Export Log" button saves to `.txt` file via save dialog.
- Log cleared on new session start; previous session log accessible via file.

---

## 5. UI/UX Flow & Screen Map

### 5.1 Navigation Structure

```
MainWindow
├── Dashboard (default view on launch)
│   └── Component status grid
│       └── "Quick Install" button  →  Install View
│       └── "Advanced" button       →  Install View (advanced mode)
├── Doctor
│   └── Check results list
│       └── Per-item "Fix" button
├── Storage
│   └── Category breakdown
│       └── Item list with sizes
│       └── "Preview Cleanup" button → Confirmation dialog → Cleanup
└── [Bottom] Command Log panel (persistent, collapsible)
```

### 5.2 Screen Descriptions

**Dashboard**

- Grid/list of all components: icon, name, status badge (Found/Outdated/Missing), version.
- Summary bar: "X of Y components ready".
- "Run Doctor" shortcut button.
- "Quick Install" (prominent CTA) and "Advanced Install" secondary button.
- Auto-runs on launch; "Re-scan" button available.

**Install View**

- Quick mode: single progress list, steps expand as they run.
- Advanced mode: checklist with version dropdowns before starting.
- Per-step status icon (spinner → check / X).
- Live command output expandable per step.
- "Retry" button on failed steps.
- Completion summary with health check link.

**Doctor View**

- List of checks grouped: Dependencies | Environment Variables | Drivers.
- Each row: icon (✓ / ⚠ / ✗), check name, description.
- Failed rows highlighted; "Fix" button where auto-fix available.
- "Re-run" button to recheck all.

**Storage View**

- Top-level: donut/bar chart of disk usage by category.
- Table: Category | Name | Size | Action.
- Multi-select for batch delete.
- "Preview Cleanup" shows a confirmation modal with total space freed.

**Command Log Panel**

- Fixed-height scrollable console at bottom.
- Collapse/expand toggle.
- Auto-scroll to latest; "Scroll to top" button.
- Copy / Export controls.

### 5.3 High-Level UX Flow

```
Launch
  └─► Auto-Detection scan runs
        └─► Dashboard shows results
              ├─► All present → "Run Doctor" CTA
              └─► Missing items → "Quick Install" CTA
                    └─► Install runs with live log
                          └─► Complete → Doctor runs automatically
                                └─► Pass → Done
                                └─► Fail → Failures highlighted with Fix buttons
                                      └─► User fixes → Re-run Doctor
```

---

## 6. Component Detection Specification

| Component | Command | Parse Target | Min Version | macOS only |
|---|---|---|---|---|
| Node.js | `node --version` | `v18.0.0` format | 18.x | No |
| npm | `npm --version` | `10.0.0` format | 9.x | No |
| JDK | `java -version` (stderr) | `openjdk version "21.0.x"` | 11 | No |
| JAVA_HOME | env var read | path exists + `bin/java` present | — | No |
| ADB | `adb version` | `Android Debug Bridge version X.Y.Z` | 34 | No |
| ANDROID_HOME | env var read | path exists + `platform-tools/` present | — | No |
| sdkmanager | `sdkmanager --version` | version string | — | No |
| Xcode | `xcodebuild -version` | `Xcode 15.x` | 14 | Yes |
| Xcode CLT | `xcode-select -p` | path exists | — | Yes |
| Appium | `appium --version` | `2.x.x` | 2.0 | No |
| UiAutomator2 driver | `appium driver list --installed` | `uiautomator2` in output | — | No |
| XCUITest driver | `appium driver list --installed` | `xcuitest` in output | — | Yes |
| Appium Inspector | app bundle path check | file exists | — | No |

**Probe timeout:** 5 seconds per command. Timeout = `NotFound`.

---

## 7. Installation Sequence Specification

### 7.1 Package Manager Matrix

| OS | Package Manager | Notes |
|---|---|---|
| macOS | Homebrew (`brew`) | Install Homebrew first if absent |
| Windows | winget | Available on Win 10 1709+ |
| Linux (Ubuntu) | apt | `sudo apt-get install` |
| Linux (Fedora) | dnf | `sudo dnf install` |

### 7.2 Per-Component Install Commands

**Node.js**

| OS | Command |
|---|---|
| macOS | `brew install node` |
| Windows | `winget install OpenJS.NodeJS.LTS` |
| Linux | `sudo apt-get install -y nodejs npm` |

**JDK 21**

| OS | Command |
|---|---|
| macOS | `brew install --cask temurin@21` |
| Windows | `winget install EclipseAdoptium.Temurin.21.JDK` |
| Linux | `sudo apt-get install -y temurin-21-jdk` |

**Android SDK / ADB**

| OS | Command |
|---|---|
| macOS | `brew install --cask android-commandlinetools` |
| Windows | `winget install Google.AndroidStudio` (SDK tools) |
| Linux | Download cmdline-tools zip, unzip to `~/Android/Sdk` |

**Appium Server**

```bash
npm install -g appium
```

**UiAutomator2 Driver**

```bash
appium driver install uiautomator2
```

**XCUITest Driver (macOS only)**

```bash
appium driver install xcuitest
```

**Appium Inspector**

| OS | Command |
|---|---|
| macOS | `brew install --cask appium-inspector` |
| Windows | Download `.exe` from GitHub releases; run installer |
| Linux | Download `.AppImage` from GitHub releases; `chmod +x` |

### 7.3 Environment Variable Configuration

| Variable | Value | macOS/Linux | Windows |
|---|---|---|---|
| `ANDROID_HOME` | `~/Android/Sdk` | Append to `~/.zshrc` / `~/.bashrc` | `setx ANDROID_HOME ...` |
| `JAVA_HOME` | JDK install path | Append to shell rc file | `setx JAVA_HOME ...` |
| `PATH` additions | `$ANDROID_HOME/platform-tools`, `$JAVA_HOME/bin` | Append to shell rc file | Modify user PATH via registry |

---

## 8. Storage & Cleanup Specification

### 8.1 Discovery Paths

| Category | Discovery Method | Default Path |
|---|---|---|
| Android AVDs | `avdmanager list avd` | `~/.android/avd/` |
| iOS Simulator runtimes | `xcrun simctl list runtimes --json` | `/Library/Developer/CoreSimulator/Profiles/Runtimes/` |
| iOS Simulator devices | `xcrun simctl list devices --json` | `~/Library/Developer/CoreSimulator/Devices/` |
| npm cache | `npm config get cache` | `~/.npm/` |
| Gradle caches | Directory scan | `~/.gradle/caches/` |
| Xcode DerivedData | Directory scan | `~/Library/Developer/Xcode/DerivedData/` |
| Appium logs/temp | Directory scan | `~/.appium/` |

### 8.2 Size Calculation

- Directories: recursive size via `Directory.GetFiles()` + `FileInfo.Length`.
- Run on background thread; update UI incrementally.
- Display in human-readable units (GB / MB).

### 8.3 Deletion Safety

1. Mark AVDs with names matching known test patterns as "safe"; named user AVDs as "review first".
2. iOS simulator runtimes not matching the installed Xcode version = safe to delete.
3. All cache directories = safe to delete.
4. Every delete previewed in a modal: item list + total size freed + "Confirm" / "Cancel".
5. Log every delete command issued.

---

## 9. Development Phases & Milestones

### Phase 1 — Core Infrastructure (Weeks 1–2)

- [x] Scaffold Avalonia solution with Core / UI / Tests separation
- [x] Implement `CommandRunner` with async stdout/stderr streaming
- [x] Implement `IPlatformAdapter` with Windows / macOS / Linux stubs
- [x] Implement `EnvironmentVariableManager` per OS
- [x] Wire up `LogService` with Serilog
- [x] Basic `MainWindow` with navigation shell

**Exit criterion:** Can spawn a CLI process and stream its output to a console view.

---

### Phase 2 — Detection & Dashboard (Weeks 3–4)

- [x] Implement `DetectionService` for all 8 components
- [x] `DashboardView` + `DashboardViewModel` bound to detection results
- [x] Auto-scan on launch
- [x] Manual Re-scan button
- [x] Component status grid with version display and state badges

**Exit criterion:** Dashboard accurately reports all installed components on all 3 platforms.

---

### Phase 3 — Installation (Weeks 5–7)

- [x] `InstallerService` with ordered, idempotent install steps
- [x] Quick Preset mode (one-button full install)
- [x] Advanced mode (component selection)
- [x] Per-OS install commands for all components
- [x] Environment variable configuration post-install
- [x] `InstallView` with live progress, step status, command expansion
- [x] Failure surfacing with retry

**Exit criterion:** Quick Preset installs a full Appium environment from scratch on all 3 platforms.

---

### Phase 4 — Doctor (Week 8)

- [x] `DoctorService` implementing all health checks
- [x] `DoctorView` with pass/warn/fail indicators
- [x] Auto-fix for fixable checks
- [x] Auto-run doctor after install completes

**Exit criterion:** Doctor view correctly diagnoses a misconfigured environment and auto-fixes env var issues.

---

### Phase 5 — Storage & Cleanup (Weeks 9–10)

- [x] `StorageService` discovery for all categories *(iOS runtimes/devices via `simctl` and stale-driver detection deferred; caches, logs, Gradle, AVDs, system images, DerivedData, Homebrew shipped)*
- [x] `CleanupService` with dry-run and confirmed-delete modes *(preview modal = the dry run)*
- [x] `StorageView` with visual breakdown and item list
- [x] Confirmation modal before any deletion
- [x] Deletion via CLI where a tool command exists (`npm`, `avdmanager`); safe validated filesystem walk otherwise *(amended from "CLI commands only" — no CLI exists for most cache categories, and freed bytes are measured, never assumed)*

**Exit criterion:** Storage view shows accurate sizes; cleanup removes selected items without touching non-selected ones.

---

### Phase 6 — Polish & Packaging (Weeks 11–12)

- [x] Keyboard navigation pass (full accessibility) *(5 global shortcuts + AutomationProperties on every interactive element via Redesign R1; a full tab-order audit on every screen remains open)*
- [x] Icon-based status indicators (not color-only) *(all status pills carry text labels; glyph+label convention throughout)*
- [x] Error handling hardening and edge case coverage *(CommandRunner missing-executable, fault-isolated scans/cleanups, never-throw stores; ongoing)*
- [x] Log export to file
- [ ] Velopack-based installer for Win / macOS / Linux
- [ ] macOS code signing + notarization *(resolved as "skip for V1" — Open Question #6)*
- [ ] Performance pass (launch < 3s, scan < 10s)

**Exit criterion:** App installs, runs, and passes health check on a clean Windows 10, macOS 12, and Ubuntu 22.04 machine.

---

## 10. Test Strategy

### Unit Tests (AppiumSetupManager.Tests)

| Area | What to test |
|---|---|
| `DetectionService` | Correct parsing of each CLI output format (mock `CommandRunner`) |
| `InstallerService` | Correct step ordering; skip logic for already-installed components |
| `DoctorService` | Each check passes/fails correctly given mocked detection results |
| `StorageService` | Size calculation, path discovery, safe-to-delete classification |
| `EnvironmentVariableManager` | Correct env var read/write per platform |
| `CommandRunner` | Stdout/stderr streaming, timeout handling, exit code capture |

### Integration Tests

| Scenario | How |
|---|---|
| Full detection scan | Run on CI machine with known installed tools; assert expected versions |
| Install single component | Install Node.js into a temp PATH-isolated environment |
| Doctor pass after install | Install full stack, run doctor, assert all pass |

### Manual Smoke Tests (per release)

| Test | Platform |
|---|---|
| Fresh install (no Appium tools present) — Quick Preset | Win, macOS, Linux |
| Re-run install (idempotent — no re-installs) | Win, macOS, Linux |
| Doctor shows all-pass after Quick Preset | Win, macOS, Linux |
| Storage view shows correct sizes | Win, macOS, Linux |
| Cleanup removes only selected items | Win, macOS, Linux |
| iOS steps hidden on Windows/Linux | Win, Linux |
| Log exports correctly | All platforms |

### Success Metrics Validation

| Metric | Measurement Method |
|---|---|
| Environment ready in < 15 min | Time Quick Preset on a clean VM |
| Health check pass rate on first run | Track doctor results in integration test pipeline |
| Disk space reclaimed | Compare before/after storage view in smoke test |

---

## 11. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| iOS setup macOS-only | Certain | Medium | Hide/disable all iOS UI elements on Windows and Linux via platform check at startup |
| Package manager unavailability | Medium | High | Detect Homebrew/winget/apt presence; prompt user to install or fall back to direct download |
| Elevation / sudo prompts | High | Medium | Request elevation only when needed; explain why in UI before prompting |
| Version pinning vs latest | Medium | Medium | Default to pinned known-good versions; expose version override in Advanced mode |
| AVD ownership ambiguity | Medium | High | Default all named AVDs to "requires review"; only auto-mark obviously-stale ones as safe |
| macOS notarization | Medium | High | Set up Apple Developer certificate early; build notarization into CI pipeline from Phase 6 |
| Regex brittleness in CLI parsing | Medium | Medium | Write version-parsing unit tests per tool; update regex when tools release new output formats |
| Slow detection scan | Low | Low | Parallel probes with 5s timeout; cache results between views |

---

## 12. Open Questions Tracker

| # | Question | Owner | Status | Resolution |
|---|---|---|---|---|
| 1 | iOS on non-macOS: hide vs disable vs separate tab? | Ray | **Resolved** | **Hide completely** — iOS components do not appear at all on Windows/Linux. |
| 2 | Package manager choice: OS PM vs bundled installers? | Ray | **Resolved** | **OS PM first (brew/winget/apt), fallback to direct download** if PM is absent. |
| 3 | Elevation model: per-command or once at launch? | Ray | **Resolved** | **Detect upfront + prefer user-scope installs.** Check at launch if any step needs elevation; request it once. Where possible, install to user home to avoid elevation entirely. |
| 4 | Version pinning: fixed manifest or configurable? | Ray | **Resolved** | **Pinned defaults; user can override in Advanced mode.** Quick Preset uses a tested version matrix. |
| 5 | AVD ownership: heuristic for junk vs wanted AVDs? | Ray | **Resolved (superseded)** | ~~Auto-mark as safe if not used in 30+ days~~ **Superseded by PRD §23.9 AC-2 during implementation: named AVDs are never auto-marked safe.** Unused >30 days → "Review first" (amber, user decides); recently used → "Recently used" (not selectable). Only cache/log categories are auto-safe. |
| 6 | macOS signing: individual dev cert or org cert? | Ray | **Resolved** | **Skip signing for now.** Ship unsigned for internal team use; users open via right-click → Open. Add signing later. |
| 7 | Localization architecture: resource files from day 1? | Ray | **Resolved** | **Yes — resource files from day 1.** All strings go through resource files even though only English ships at launch. |

---

## 13. Visual Redesign R1/R2 (July 2026) — Status Record

A full visual and functional overhaul, implemented after Phases 1–5, from the claude.ai/design
project "Appium Setup Manager UI" (Organic design system, sage/green accent `#659287`,
Caprasimo/Figtree typography, pill-radius language, dual light/dark theme). Not part of the
Phase 1–6 numbering above — referred to as "R1"/"R2" in commits and code comments.

**R1 (6 steps):** theme tokens + embedded fonts + light/dark toggle + 9-item nav shell; reskin of
Dashboard/Installation/Doctor/Storage; real Environment screen (env-var editor with backup/restore);
real Updates screen (npm-registry version checks); real History screen (persistent audit log at
`~/.appiumsetupmanager/history.json`, rollback only for env-var Setup entries); Logs screen
(filterable view over the shared log buffer) + Settings screen + settings persistence
(`~/.appiumsetupmanager/settings.json`) + 5 working global keyboard shortcuts.

**R2:** real `StorageService`/`CleanupService` (see amended Phase 5 above); log export; top-bar
quick-search palette (screens/components/logs); PRD §39 design tokens updated to match; app icon
(`Assets/Icons/` — ico/icns/png generated from the design's brand glyph); font OFL licenses bundled.

**Design-honesty rule established during R1** (binding for future work): no UI element ships that
looks functional but isn't — controls without a real backing capability are omitted or visibly
disabled, settings persist only if they are (or will be) genuinely consumed, and reported numbers
(freed bytes, versions, health scores) are always measured, never assumed.

**Known open items:** Velopack packaging + performance pass + clean-machine validation (Phase 6);
notification mechanism to consume the Notify on Failure/Completion settings; snapshot/grace-period
restore for cleanup; Dashboard tile "Open folder"/"Repair" commands; full per-screen tab-order audit.

---

*End of Project Documents — v1.0*
