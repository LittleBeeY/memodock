<div align="center">
  <img src="./src/MemoDock/Assets/MemoDock.svg" width="96" alt="MemoDock icon">
  <h1>MemoDock</h1>
  <p>A local notebook for every Windows app.</p>
  <p><strong>Windows 11 · WPF · .NET 10 · Local-first</strong></p>
  <p>
    <a href="./LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="License: Apache 2.0"></a>
    <img src="https://img.shields.io/badge/.NET-10-purple.svg" alt=".NET 10">
    <img src="https://img.shields.io/badge/windows-11-0078D6.svg" alt="Windows 11">
  </p>
  <p>
    <a href="./README.md">简体中文</a> · <strong>English</strong>
  </p>
</div>

MemoDock detects the app that was last in the foreground and automatically switches to that app's own notes and todos. Everything is stored locally — no account or network required.

> Current version: `0.3.0` ([Download](https://github.com/LittleBeeY/memodock/releases/tag/v0.3.0))

<p align="center">
  <img src="./output/imagegen/memodock-sidebar-v5.png" width="360" alt="MemoDock preview">
</p>

## Features

- Per-app notes isolated by stable identity; notes survive Store app updates
- Auto-follows the foreground app; can also lock and switch manually
- Keeps the current app when clicking the taskbar or desktop
- Two record types: notes and todos
- Search, create, edit and delete within the current app
- Todo completion state
- `Ctrl + Alt + N` global hotkey
- Single instance; relaunch only brings up the existing window
- Drag, resize, and remembered window size/position
- Closes to the system tray; tray menu can export a data backup
- Windows 11 Desktop Acrylic, rounded corners and dark theme
- Previous data is preserved before overwrite; corrupt files trigger a backup restore
- No account, no telemetry, no cloud dependency

## Quick Start

### Run from source

Requirements:

- Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
dotnet run --project .\src\MemoDock\MemoDock.csproj
```

### Usage

1. Keep MemoDock running in the background.
2. Switch to the app you want to take notes for.
3. Press `Ctrl + Alt + N`, or double-click the tray icon to open MemoDock.
4. Choose "Note" or "Todo" and add a record for the current app.
5. Click the app name at the top to switch manually; turn off "Auto" to lock the current app.
6. The close button and `Esc` only hide the window; use the tray "Exit" to quit.
7. For an extra backup, choose "Export data backup…" from the tray menu.

The hotkey only shows the window — it never auto-opens the record editor.

## Data & Privacy

MemoDock never uploads your records and depends on no network service. Data is stored by default at:

| File | Purpose |
| --- | --- |
| `%LOCALAPPDATA%\MemoDock\memos.json` | Current notes and todos |
| `%LOCALAPPDATA%\MemoDock\memos.json.bak` | Previous data before overwrite |
| `%LOCALAPPDATA%\MemoDock\window.json` | Window size and position |

Saving writes a temporary file first, then atomically replaces the target. If the main JSON is corrupt, the original file is kept under a new name and the `.bak` is restored automatically. Old data is migrated on load: normal desktop apps keep the full EXE path as their key, while Store apps use a version-stable identity and records from before/after upgrades are merged. A `.bak` is also produced before migration.

> "Local-private" means the data never leaves this computer; records are currently stored as plaintext JSON, which is not the same as encrypted storage.

## Development

### Build & test

```powershell
dotnet restore .\MemoDock.sln --configfile .\NuGet.Config
dotnet build .\MemoDock.sln --configuration Release --no-restore
dotnet run --project .\tests\MemoDock.CoreTests\MemoDock.CoreTests.csproj --configuration Release --no-restore
```

Current self-tests cover:

- Per-app record isolation and persistence
- Title and body search
- Corrupt-data recovery from the previous version
- Backup retention before saving
- Data export copies
- Stable identity and migration for Store app upgrades

### Publishing

The publish script reads the version from `Directory.Build.props` automatically:

```powershell
# Framework-dependent multi-file (default) — share the whole folder
.\scripts\publish.ps1

# Self-contained single-file (win-x64) — no .NET preinstall needed
.\scripts\publish.ps1 -SelfContained

# Self-contained single-file (ARM64)
.\scripts\publish.ps1 -SelfContained -Runtime win-arm64
```

Output goes to `.\artifacts\`. The self-contained build enables ReadyToRun for faster startup and is tied to a specific CPU architecture; code trimming is disabled because the tray icon relies on WinForms.

The repo's `NuGet.Config` intentionally clears remote package sources; the publish script temporarily points to the official NuGet source for runtime packages.

## Project Structure

```text
src/
  MemoDock.Core/       Data models, queries, migration, local storage (platform-agnostic)
    Models/            Memo, notebook, record models
    Services/          MemoRepository, MemoQuery, MemoMigrator, AppIdentity, etc.
  MemoDock/            WPF UI and Windows integration
    Assets/            App icon sources and ICO
    Services/          Foreground detection, hotkey, single-instance, window effects, menus
tests/
  MemoDock.CoreTests/  Framework-free core logic self-tests
scripts/
  publish.ps1          One-click publish (framework-dependent / self-contained)
output/imagegen/       Approved visual baseline
Directory.Build.props  Central project version
```

## Current Limitations

- Different workspaces of the same app currently share one set of records.
- A few apps that use system process wrappers and hide the real EXE path may display inaccurately.
- First open docks to the right of the primary work area; it does not yet follow the foreground app's monitor.
- The global hotkey is fixed to `Ctrl + Alt + N`; there is no settings UI yet.
- No MSIX installer, code signing, or startup-launch setup yet.

## Contributing

Issues and pull requests are welcome. Please read the [Contributing Guide](./CONTRIBUTING.en.md) first:

- Bug or feature request → open an [Issue](../../issues/new/choose)
- Code changes → fork and submit a [Pull Request](../../pulls)
- Security vulnerability → follow [SECURITY.en.md](./SECURITY.en.md) for private channels

## License

[Apache License 2.0](./LICENSE) © 2026 MemoDock contributors

Records are stored as plaintext JSON — "local-private" rather than encrypted. Do not write sensitive credentials into records.
