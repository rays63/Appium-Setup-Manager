<div align="center">

<img src="docs/icon.png" width="96" alt="Appium Setup Manager logo" />

# Appium Setup Manager

**A working Appium environment, without the wiki page.**

Detect, install, repair and maintain your entire mobile-automation toolchain —
Node, JDK, Android SDK, Appium and its drivers — from one desktop app.

</div>

---

## What is Appium Setup Manager?

Setting up Appium by hand means installing and wiring together **eight-plus tools** —
Node.js, a JDK, the Android SDK and ADB, Xcode command-line tools, the Appium server,
UiAutomator2/XCUITest drivers, Appium Inspector — each with its own version pitfalls,
environment variables and OS-specific gotchas. It's the single biggest source of
onboarding friction for QA teams, and the reason "works on my machine" haunts
automation projects.

Appium Setup Manager is a native desktop app (**Windows, macOS, Linux**) that does the
wiring for you. It scans your machine, tells you exactly what's installed vs. what's
needed, installs what's missing through the same official CLIs an expert would use,
fixes broken configuration, and shows you every command it runs — live, as it runs it.

## Features

- 🔍 **Auto-detection dashboard** — scans your whole toolchain on launch and shows a
  health ring, per-component version cards, and environment-variable status at a glance.
- ⚡ **Quick Install** — one button installs everything a working environment needs,
  in dependency order (on macOS that includes Homebrew itself if it's missing).
  Advanced mode lets you pick individual components.
- 🩺 **Doctor** — appium-doctor-style diagnostics with an overall health score,
  per-issue root cause, and one-click repair for the fixable ones.
- 🌿 **Environment management** — edit `JAVA_HOME`/`ANDROID_HOME` with automatic
  backups and one-click restore, detect existing JDK/SDK installs instead of
  re-downloading them, and add directories to `PATH` from the app (new value is live
  immediately — no restart needed).
- 🧹 **Storage cleanup** — a donut-chart breakdown of npm/Gradle/Appium caches, AVDs,
  system images, and (on macOS) Xcode junk. Cleanup shows a preview first, deletes via
  official CLIs where they exist, and reports **measured** freed bytes — never estimates.
- ⬆️ **Updates** — checks the npm registry for newer Appium/driver versions and
  updates per-component or all at once. Can be turned off entirely in Settings.
- 🕘 **History** — every install, update, repair, cleanup and environment change is
  recorded locally with a timeline view; environment changes can be rolled back.
- 🖥️ **Live command console** — every CLI invocation streams its real output into a
  resizable console panel, with a filterable full-log screen and one-click export.
- ⌨️ **Quick search & shortcuts** — `⌘K`/`Ctrl+K` jumps to any screen or component;
  five global shortcuts are documented (accurately) on the Settings screen.
- 🌗 **Light & dark themes** with a one-key toggle, and **zero telemetry** — see
  Privacy below.

### Safe by design

The app **never runs a command it doesn't show you**. Installs and deletions go
through official tooling (`npm`, `brew`, `winget`, `avdmanager`, …) with output
streamed live to the console. Cleanup refuses to touch anything outside your home and
SDK directories, never follows symlinks, never auto-selects your emulators (AVDs are
always flagged for review — recently-used ones can't be selected at all), and always
shows a confirmation preview with exact sizes before anything is removed.

### Privacy

Everything the app records — settings, install history, environment backups, logs —
lives in local files under your home directory and is never sent anywhere. The only
network access is the npm update check (which you can disable) and the downloads you
explicitly request.

## Platforms

| OS | Support |
|---|---|
| macOS 12+ | Full (Android + iOS toolchains) |
| Windows 10+ | Android toolchain |
| Linux (Ubuntu 22.04+) | Android toolchain |

iOS tooling (Xcode, XCUITest, simulators) exists only on macOS, so those components
are hidden entirely on other platforms — not shown disabled.

## Download & run

Tagged releases are built by CI for **win-x64, win-arm64, osx-x64, osx-arm64 and
linux-x64** and attached to the [**Releases**](../../releases) page as self-contained
zips — no .NET installation required to run them.

- **Windows** — unzip and run `AppiumSetupManager.exe`.
- **macOS** — builds aren't signed/notarized yet, so clear the quarantine flag once:

  ```bash
  xattr -cr AppiumSetupManager
  chmod +x AppiumSetupManager
  ./AppiumSetupManager
  ```

  Or build the proper `.app` bundle from source (see below) for a Dock icon and
  Finder-friendly launch.
- **Linux** — unzip, then `chmod +x AppiumSetupManager && ./AppiumSetupManager`.

## Feature requests & bugs

- 💡 **Missing something?** If it fits the app's goal — getting and keeping an Appium
  environment healthy — [**open a feature request**](../../issues/new?template=feature_request.md).
- 🐛 **Something broke?** [**Open a bug report**](../../issues/new?template=bug_report.md)
  and attach the exported log from the console panel — it makes fixes dramatically faster.

Check the [existing issues](../../issues) first in case it's already been raised.

## Roadmap

- Velopack-based installers with in-app updates (CI zips exist today).
- macOS code signing + notarization.
- Desktop notifications for install/repair completion and failure (the Settings
  toggles for these are already persisted).
- Cleanup snapshots — a grace period to undo a storage cleanup.
- Localization (every user-facing string already lives in resource constants).

---

## For developers

Avalonia UI 11 app (MVVM, CommunityToolkit.Mvvm) in three projects: `Core` holds all
business logic behind service interfaces with **zero UI dependencies**; per-OS
behavior is isolated in `IPlatformAdapter` implementations; the UI project contains
only views, view-models and theming.

### Build & run from source

Requires the **.NET 10 SDK**.

```bash
dotnet run --project src/AppiumSetupManager/AppiumSetupManager.csproj
```

### Tests

```bash
dotnet test    # 143 tests: services, stores, platform logic — all runnable offline
```

### Publish a self-contained binary

```bash
# RIDs: win-x64, win-arm64, osx-x64, osx-arm64, linux-x64
dotnet publish src/AppiumSetupManager/AppiumSetupManager.csproj \
  -c Release -r osx-arm64 --self-contained -o artifacts/publish-osx-arm64
```

### macOS .app bundle

```bash
scripts/make-macos-bundle.sh    # assembles "Appium Setup Manager.app" from the publish output
```

CI ([`.github/workflows/build.yml`](.github/workflows/build.yml)) builds all five
targets on every push and attaches zipped binaries to tagged releases.

### Project layout

| Path | What's there |
|---|---|
| `src/AppiumSetupManager.Core/Services` | Detection, install, doctor, storage, cleanup, update-check services |
| `src/AppiumSetupManager.Core/Infrastructure` | CommandRunner, env-var manager, atomic JSON stores (settings/history/backups) |
| `src/AppiumSetupManager.Core/Platform` | `IPlatformAdapter` + per-OS implementations |
| `src/AppiumSetupManager/ViewModels` | MVVM state & commands |
| `src/AppiumSetupManager/Views` | Avalonia XAML screens + the command console |
| `src/AppiumSetupManager/Themes` | Design tokens, light/dark dictionaries, control styles, icons |
| `src/AppiumSetupManager.Tests` | xUnit + FluentAssertions + NSubstitute |

### Contributing

1. Fork and branch from `main`.
2. Business logic goes in `Core` behind an interface — no Avalonia references there.
3. No fake UI: a control either does something real or doesn't ship.
4. `dotnet test` must pass before opening a PR; the PR template has the checklist.

## License

MIT — see [LICENSE](LICENSE).
