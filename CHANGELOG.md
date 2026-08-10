# 更新日志

> **English**: [CHANGELOG.en.md](./CHANGELOG.en.md)

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 约定，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]

### 新增

- 跨软件全局搜索：开启后一次搜索全部软件的笔记与待办，结果标注所属软件。
- 回收站：删除改为软删除，记录可在回收站中恢复或彻底删除（数据库结构 v3）。
- 滚动备份：覆盖前保留 `.bak` 与 `.bak.1` 两级历史，主备份损坏时自动回退更早备份。
- 多显示器：首次停靠跟随前台软件所在显示器右侧，按该显示器 DPI 换算坐标。
- 主窗口键盘快捷键：`Ctrl + N` 新建记录、`Ctrl + F` 聚焦搜索框。

### 改进

- 搜索支持多个关键词（以空白分隔，全部命中）；待办未完成项排在已完成项之前。
- 保存增加防抖合并写盘，隐藏窗口或退出时强制落盘未保存的修改。

### 修复

- 修复图标提取泄漏 HICON 句柄的问题，长时间运行不再耗尽 GDI 资源。
- 前台轮询改为先比对窗口句柄、图标按路径缓存，避免每 800ms 重复读取 exe 元数据。

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
