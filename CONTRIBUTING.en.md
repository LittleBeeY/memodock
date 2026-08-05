# Contributing Guide

Thanks for your interest in MemoDock! Bug fixes, features, documentation improvements and suggestions are all welcome.

## About the Project

MemoDock is a Windows 11 native WPF app (.NET 10) that keeps local notes and todos isolated per foreground app (keyed by executable path). It **does not use accounts or network services** — all data stays on this machine.

- **Local-first**: no cloud sync, no telemetry, no accounts.
- **Zero third-party dependencies**: `MemoDock.Core` references no NuGet packages.
- **Layered architecture**: `MemoDock.Core` (platform-agnostic logic) → `MemoDock` (WPF UI & Windows integration).

## Development Environment

- Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build & Test

```powershell
dotnet restore .\MemoDock.sln --configfile .\NuGet.Config
dotnet build .\MemoDock.sln --configuration Release --no-restore
dotnet run --project .\tests\MemoDock.CoreTests\MemoDock.CoreTests.csproj --configuration Release --no-restore
```

> The tests are a self-contained console app with no xUnit/NUnit or other third-party frameworks, to keep the project dependency-free. Core logic changes must update the self-tests in `tests/MemoDock.CoreTests/Program.cs`.

## Code Boundaries

- `src/MemoDock.Core/` holds only platform-agnostic data models, queries, migration and local storage.
  - Database migrations live in `Services/MemoMigrator.cs`.
  - Path constants are centralized in `Services/AppPaths.cs`.
  - Atomic file writes use `Services/AtomicFile.cs`.
- `src/MemoDock/` holds the WPF UI, Win32 foreground-window detection, global hotkey, system tray and window effects.
  - Context menus are built by `Services/ContextMenuBuilder.cs`.

## Product Behavior That Must Be Preserved

Make sure these do not regress:

- `Ctrl + Alt + N` only shows and activates the main window; it never opens the editor.
- Single instance; a second launch only activates the existing instance.
- The close button and `Esc` only hide to the tray; only the tray "Exit" ends the process.
- Data lives in `%LOCALAPPDATA%\MemoDock\`; never silently upload or add cloud dependencies.
- Keep `memos.json.bak` before overwriting; prefer restoring the backup when the main file is corrupt.
- Normal apps use the normalized full EXE path as the key; Store apps use a version-stable identity.
- The main window keeps top drag and all four edge/corner resize handles.

## Submitting a PR

1. Fork this repo and create a feature branch.
2. Keep changes focused: one PR, one problem.
3. Ensure `dotnet build` has no warnings and all tests pass.
4. Describe your motivation and how you verified the change.
5. Wait for review, or contact the maintainer directly.

## Code Style

- Follow the existing style; match naming, indentation and comments around you.
- Add XML doc comments to public APIs.
- Do not commit `bin/`, `obj/`, `artifacts/`, `.dotnet-cli/`, `.dotnet-sdk/`, `tmp/` or `*.log`.
- Do not commit real memo data, user path dumps, secrets, or account information.
