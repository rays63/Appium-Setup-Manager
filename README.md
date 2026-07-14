# Appium Setup Manager

A cross-platform desktop application that eliminates the manual, error-prone process of configuring an Appium mobile automation environment. One button press detects what is installed, installs what is missing, verifies environment health, and helps reclaim disk space.

> **Status:** Pre-release — Phase 1 in development.

---

## Why

Setting up Appium today requires manually installing and wiring together 8+ tools — Node.js, JDK, Android SDK/ADB, Xcode command-line tools, Appium server, UiAutomator2, XCUITest, and Appium Inspector — each with OS-specific version pitfalls and environment variables. This is the leading source of onboarding friction for QA teams.

Appium Setup Manager replaces that process with a GUI that runs every action through the underlying CLI, streams the output live, and never touches your machine without showing you exactly what it is about to do.

---

## Features

| Feature | Description |
|---|---|
| **Auto-detection** | On launch, scans all Appium-related tools and reports installed versions vs required versions |
| **One-click install** | Quick Preset installs a complete, working environment with sensible defaults |
| **Advanced install** | Select individual components and pin specific versions |
| **Health check** | Diagnostics equivalent to `appium-doctor`, displayed visually with one-click auto-fix |
| **Storage view** | Audits disk usage by category (emulators, simulators, caches, SDK junk) |
| **Safe cleanup** | Removes selected items via CLI; nothing deleted without explicit confirmation |
| **Command log** | Every CLI command and its output is streamed live and exportable |

---

## Platforms

| OS | Support |
|---|---|
| macOS 12+ | Full (Android + iOS) |
| Windows 10+ | Android only |
| Ubuntu / Fedora | Android only |

iOS setup (Xcode, XCUITest, simulators) is macOS-only and hidden on other platforms.

---

## Tech Stack

- **UI:** [Avalonia UI 11](https://avaloniaui.net/) (XAML / MVVM)
- **Language:** C# 12 / .NET 8
- **MVVM:** CommunityToolkit.Mvvm
- **Logging:** Serilog
- **Tests:** xUnit + FluentAssertions + NSubstitute
- **Packaging:** Velopack

---

## Project Structure

```
AppiumSetupManager.sln
└── src/
    ├── AppiumSetupManager/          # Avalonia UI — views, viewmodels, assets
    ├── AppiumSetupManager.Core/     # Business logic — services, models, platform adapters
    └── AppiumSetupManager.Tests/    # xUnit test project
```

The `Core` project has no UI dependency and is fully unit-testable in isolation. Platform differences (macOS / Windows / Linux) are isolated behind `IPlatformAdapter`.

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
# macOS
brew install dotnet

# Windows
winget install Microsoft.DotNet.SDK.8

# Linux (Ubuntu)
sudo apt-get install -y dotnet-sdk-8.0
```

### Clone and build

```bash
git clone https://github.com/rays63/Appium-Setup-Manager.git
cd appium-setup-manager
dotnet restore
dotnet build
```

### Run

```bash
dotnet run --project src/AppiumSetupManager/AppiumSetupManager.csproj
```

### Run tests

```bash
dotnet test src/AppiumSetupManager.Tests/AppiumSetupManager.Tests.csproj
```

---

## Development Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Core infrastructure — `CommandRunner`, platform adapters, navigation shell | In progress |
| 2 | Environment detection + Dashboard view | Planned |
| 3 | Installation — Quick Preset and Advanced mode | Planned |
| 4 | Health check (Doctor) view | Planned |
| 5 | Storage view and cleanup | Planned |
| 6 | Polish, packaging, macOS signing | Planned |

---

## Contributing

1. Fork the repository and create a branch from `main`.
2. Keep business logic in `AppiumSetupManager.Core` — no Avalonia references there.
3. New services must have a matching interface (e.g. `IDetectionService`) for testability.
4. Run `dotnet test` before opening a pull request.
5. Open an issue first for anything beyond a small bug fix.

---

## License

MIT — see [LICENSE](LICENSE).
