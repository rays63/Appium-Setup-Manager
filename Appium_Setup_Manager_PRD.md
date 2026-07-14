# Product Requirements Document — Appium Setup Manager

*A cross-platform GUI for automated Appium environment setup, diagnostics, and cleanup*

**Version 1.0 (Draft) · July 14, 2026**
**Author: Ray · Status: For Review**

---

## 1. Overview

Appium Setup Manager is a desktop application that eliminates the manual, error-prone process of configuring an Appium test environment. Setting up Appium today requires QA engineers to manually install Node.js, the JDK, Android SDK/ADB, Xcode command-line tools, the Appium server, drivers (UiAutomator2, XCUITest), Appium Inspector, and to correctly wire environment variables (`ANDROID_HOME`, `JAVA_HOME`, `PATH`). Each of these has version and OS-specific pitfalls.

This tool provides a friendly UI that performs every action through underlying CLI commands — never a manual copy-paste process. With a single button press, it detects what is already installed, installs what is missing, verifies the environment health, and helps reclaim disk space by removing unnecessary files (caches, stale emulators/simulators, orphaned SDK components).

### 1.1 Problem Statement

- Appium setup is slow, inconsistent across machines, and a common source of onboarding friction for QA teams.
- Dependencies and env variables are easy to misconfigure and hard to debug ("works on my machine").
- Android SDKs, emulators, iOS simulators, and package caches silently consume tens of gigabytes with no easy way to audit or clean them.

### 1.2 Goals

- One-click install of a complete, working Appium environment on Windows, macOS, and Linux.
- Accurate detection of existing installs and their versions before taking any action.
- Environment health diagnostics equivalent to appium-doctor, surfaced visually.
- Transparent storage view and safe cleanup of caches, emulators/simulators, and junk.
- Every operation executed via CLI under the hood, with a live, readable command log.

### 1.3 Non-Goals

- Writing or executing user test scripts (this is a setup/maintenance tool, not a test runner).
- Replacing Appium Inspector's inspection UI — the tool installs and launches it, not reimplements it.
- Managing CI/CD pipeline setup (potential future scope).

---

## 2. Target Users

The primary user is the **QA engineer / tester** responsible for standing up and maintaining a mobile automation environment. This ranges from beginners setting up Appium for the first time to experienced testers who want a fast, repeatable, disk-conscious setup across many machines.

| Persona | Needs | How the tool helps |
|---|---|---|
| QA engineer (new) | A working setup without deep CLI/env knowledge | Quick-preset one-click install + guided diagnostics |
| QA engineer (experienced) | Speed, control, reproducibility | Advanced mode with component selection + command log |
| QA lead / onboarding | Consistent setup across the team | Deterministic installs, version reporting, health check |

---

## 3. Recommended Technology Stack

**Recommended: Avalonia (.NET / C#).** This application is roughly 80% process orchestration and 20% UI — its core job is spawning CLI processes and parsing their output. .NET's `System.Diagnostics.Process` gives mature, async stdout/stderr streaming, and C# covers the entire app (detection probes, installers, cleanup, disk APIs, env-var handling) in a single language with no frontend/backend split. That single-language, process-centric fit is why it edges out Tauri here. Tauri and Electron remain solid alternatives — see below.

| Criteria | Avalonia (recommended) | Tauri (Rust + web) | Electron |
|---|---|---|---|
| Core job: run CLI commands | Excellent — `Process` async streaming, one language end-to-end | Good — Rust backend, but split across JS/IPC boundary | Good — Node `child_process` |
| Single-language codebase | Yes (C# throughout) | No (Rust + JS/TS) | No (Node + JS/TS) |
| Cross-platform installers | Win/mac/Linux; mac signing needs manual setup | Native .msi / .dmg / .deb / AppImage | Yes (larger bundles) |
| Bundle size | ~40–80 MB (trimmed / NativeAOT) | ~3–10 MB | ~85–120 MB |
| UI talent pool | Smaller (XAML / MVVM) | Large (web / React) | Large (web / React) |
| Startup / footprint | Good with NativeAOT | Excellent | Heavier (Chromium) |
| Fit for a process-heavy tool | Very strong | Strong | Moderate |

**Decision guide:** if the team's background is C#/.NET, choose Avalonia — the bulk of the work (running commands, parsing versions, per-OS install/cleanup logic) is exactly what .NET does gracefully in one language. If the team is web/JS-first, Tauri is the better call for its lightweight footprint and familiar UI stack. Avalonia's main tradeoffs are a larger bundle than Tauri and more manual macOS notarization/signing setup.

---

## 4. Functional Requirements

### 4.1 Environment Detection

On launch, the tool scans the system and reports what is installed and each version. Detection runs via CLI probes (e.g. version flags) and known install-path checks.

| Component | Detection method | Reported |
|---|---|---|
| Node.js / npm | `node -v`, `npm -v` | Installed + version |
| JDK | `java -version`, `JAVA_HOME` | Installed + version + env var |
| Android SDK / ADB | `adb version`, `ANDROID_HOME`/`sdkmanager` | Installed + version + path |
| Xcode / CLT (macOS) | `xcodebuild -version`, `xcode-select -p` | Installed + version |
| Appium server | `appium -v` | Installed + version |
| Appium drivers | `appium driver list --installed` | UiAutomator2, XCUITest status |
| Appium Inspector | app bundle / install path check | Installed + version |
| GUI / drivers | driver + plugin inventory | Status per item |

### 4.2 Installation

Two flows are offered:

- **Quick preset** — "Install everything" button that sets up a complete, working Appium environment with sensible defaults.
- **Advanced mode** — checkboxes to select individual components and versions, for users who want fine control.

Installation requirements:

- Every install action is executed strictly through CLI commands and package managers — no manual steps required from the user.
- Only missing or outdated components are installed; already-present items are skipped (idempotent).
- Environment variables (`ANDROID_HOME`, `JAVA_HOME`, `PATH`) are configured automatically per OS.
- Appium drivers (UiAutomator2, XCUITest) and Appium Inspector are installed as part of the flow.
- Real-time progress with per-step status, and a live command log the user can expand.
- On failure, the tool surfaces the exact command, stderr output, and a suggested fix; steps are retryable.

### 4.3 Environment Health Check (Doctor)

A diagnostics view runs appium-doctor–equivalent checks and presents results visually (pass / warn / fail), covering dependency presence, versions, env variables, and driver readiness. Each failed check offers a one-click remediation where possible.

### 4.4 Storage View & Cleanup

A dedicated view audits disk usage and enables safe reclamation. Depth: **full** — caches, emulators/simulators, and SDK/junk.

| Category | Examples | Action |
|---|---|---|
| Appium / SDK junk | Stale drivers, orphaned SDK packages, old build tools | List + selective delete |
| Emulators / Simulators | Android AVDs, iOS simulator runtimes & devices | Show size, delete unused |
| Caches | npm cache, Gradle caches, temp/derived data | Clear via CLI |

- Storage view shows total and per-category usage with a visual breakdown.
- Nothing is deleted without explicit user confirmation; a preview lists exactly what will be removed and how much space it frees.
- Cleanup is performed via CLI commands (e.g. cache clean, `avdmanager`/`simctl` delete), never by touching files blindly.
- Destructive actions are guarded and clearly distinguished from reversible ones.

### 4.5 Command Log & Transparency

- A persistent, expandable console shows every CLI command issued and its output.
- Logs are exportable (for sharing setup issues with a team or filing tickets).

---

## 5. Non-Functional Requirements

| Category | Requirement |
|---|---|
| Platforms | Windows 10+, macOS 12+, major Linux distros (Ubuntu/Fedora) |
| Performance | App launch < 3s; detection scan < 10s on a typical machine |
| Safety | No deletion without confirmation; dry-run preview for cleanup |
| Permissions | Requests elevation only when required (e.g. PATH edits); explains why |
| Offline behavior | Detection & storage view work offline; installs require network |
| Accessibility | Keyboard navigable, readable status colors with icons (not color-only) |
| Localization | English at launch; architecture supports future locales |

---

## 6. High-Level UX Flow

1. Launch → tool auto-runs detection and shows a dashboard of component status + versions.
2. User chooses Quick preset (install everything) or Advanced mode (select components).
3. Tool installs via CLI, streaming progress and a live command log.
4. Health check runs; failures show one-click fixes.
5. User opens Storage view to audit and clean up space when needed.

---

## 7. Success Metrics

- Time from launch to a fully working Appium environment (target: under 15 minutes unattended).
- % of setups that pass the health check on first run.
- Average disk space reclaimed per cleanup session.
- Reduction in setup-related support requests within a team.

---

## 8. Open Questions & Risks

| Item | Note |
|---|---|
| iOS on non-macOS | XCUITest/Xcode setup is macOS-only; UI must gracefully hide/disable iOS on Win/Linux. |
| Package manager choice | Use OS package managers (Homebrew/winget/apt) or bundle installers? Affects elevation & reliability. |
| Elevation model | How to safely edit system PATH/env vars per OS with least privilege. |
| Version pinning | Should installs pin known-good versions or take latest? Impacts reproducibility. |
| Emulator ownership | Distinguishing user-created AVDs from junk to avoid deleting wanted devices. |

---

*End of document — v1.0 draft for review.*
