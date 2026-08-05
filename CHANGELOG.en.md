# Changelog

This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Internal

- Added English translations for user-facing docs (README, contributing guide, security policy, changelog) with language switcher links.

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
