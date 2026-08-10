# Changelog

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Global search across all apps; results are labeled with their owning app.
- Recycle bin: deletion is now a soft delete; records can be restored or permanently removed (schema v3).
- Rolling backups: `.bak` and `.bak.1` are preserved before overwrite, and load falls back to the older backup if the newest one is corrupt.
- Multi-monitor: first dock follows the foreground app's monitor (with per-monitor DPI conversion).
- Window keyboard shortcuts: `Ctrl + N` to create a record, `Ctrl + F` to focus search.

### Improved

- Search supports multiple keywords (whitespace-separated, all must match); active todos sort before completed ones.
- Saving is debounced and batched; pending changes are flushed when the window hides or the app exits.
- Record lists are virtualized for smoother scrolling with many records.

### Fixed

- Fixed a HICON handle leak in icon extraction that could exhaust GDI resources over long sessions.
- Foreground polling now short-circuits on the window handle and caches icons per executable path, avoiding repeated exe metadata reads every 800 ms.
- Load now recovers from a backup when the main file is missing (e.g. a crash mid-save) instead of falling back to an empty database.
- Corrupt snapshots use millisecond timestamps plus a random suffix to avoid same-second collisions, and only the latest 10 are kept.
- Icon extraction swallows unexpected exceptions for untrusted paths so manual switching cannot crash on a bad path.
- Multi-monitor DPI lookup falls back to 96 DPI on failure, avoiding divide-by-zero coordinates.

### Internal

- Records now carry a creation timestamp (schema v4); old data is backfilled with the update time on load.
- Self-tests expanded: merge takes the newer entry, atomic-write backup paths, fallback to empty when all backups are corrupt, and migration backfill.
- Added English translations for user-facing docs (README, contributing guide, security policy, changelog) with language switcher links.
- Pinned third-party GitHub Actions in CI and release workflows to full commit SHAs to remove supply-chain risk from mutable tags.

## [0.3.0] - 2026-08-05

### Added

- Shareable Beta: `Ctrl + Alt + N` global hotkey, single-instance running, window drag/resize and position memory.
- Closes to the system tray; tray menu supports exporting a data backup.
- Windows 11 Desktop Acrylic, rounded corners and dark theme.
- Previous data is preserved before overwrite; corrupt main files trigger a backup restore.
- New app icon: memo notepad with dock base, teal-purple brand gradient, 7 sizes.

### Improved

- Foreground app icon extraction now falls back to the Shell API (`SHGetFileInfo`) for better coverage (browsers, etc.).
- When icon extraction fails, the first letter of the app name is shown instead of the old `{ }`.
- Open-source foundation: Apache-2.0 license, contributing guide, security policy, CI and Release workflows.
- Scripted publishing: added `scripts/publish.ps1`; version is centralized in `Directory.Build.props`.
- Self-contained publishing enables ReadyToRun for faster startup.
- Refactor: migrations extracted to `MemoMigrator`, atomic writes to `AtomicFile`, path constants to `AppPaths`, menu building to `ContextMenuBuilder`.
- XML doc comments added to public APIs; search/sort comparisons unified to `OrdinalIgnoreCase`.

### Fixed

- Store apps keep their notes after upgrades (stable identity recognition).

[Unreleased]: https://github.com/LittleBeeY/memodock/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/LittleBeeY/memodock/releases/tag/v0.3.0
