# 更新日志

> **English**: [CHANGELOG.en.md](./CHANGELOG.en.md)

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 约定，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]

### 内部调整

- 为面向用户的文档补充英文版（README、贡献指南、安全策略、变更日志）及语言切换链接。
- 固定 CI 与发布工作流中的第三方 GitHub Actions 到完整提交 SHA，消除可变标签带来的供应链风险。

## [0.3.0] - 2026-08-05

### 新增

- 可分享 Beta 版：支持 `Ctrl + Alt + N` 全局快捷键、单实例运行、窗口拖动/缩放及位置记忆。
- 关闭到系统托盘，托盘菜单支持导出数据备份。
- Windows 11 Desktop Acrylic、圆角和深色界面。
- 覆盖前自动保留上一版数据，主文件损坏时尝试恢复。
- 全新应用图标：便签纸 + 停靠底座，青紫品牌渐变，7 种尺寸。

### 改进

- 前台软件图标提取增加 Shell API（`SHGetFileInfo`）兜底，浏览器等应用提取更可靠。
- 图标提取失败时显示软件名首字母占位符，替代原先的 `{ }`。
- 开源工程基础：Apache-2.0 许可证、贡献指南、安全策略、CI 与 Release 工作流。
- 发布流程脚本化：新增 `scripts/publish.ps1`，版本号统一由 `Directory.Build.props` 管理。
- 自包含发布启用 ReadyToRun 加速启动。
- 重构：数据库迁移提取为 `MemoMigrator`，原子写提取为 `AtomicFile`，路径常量集中到 `AppPaths`，菜单构建提取为 `ContextMenuBuilder`。
- 为公开 API 补充 XML 文档注释；搜索与排序统一使用 `OrdinalIgnoreCase`。

### 修复

- 商店应用升级后仍能关联原有笔记（稳定身份识别）。

[未发布]: https://github.com/LittleBeeY/memodock/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/LittleBeeY/memodock/releases/tag/v0.3.0
