# 贡献指南

> **English**: [CONTRIBUTING.en.md](./CONTRIBUTING.en.md)

感谢你对 MemoDock 的兴趣！无论是修 bug、加功能、改进文档还是提建议，都欢迎。

## 项目简介

MemoDock 是 Windows 11 原生 WPF 应用（.NET 10），按前台程序的可执行文件路径隔离本地笔记和待办。它**不使用账户或网络服务**，所有数据保存在本机。

- **Local-first**：没有云同步、没有遥测、没有账户。
- **零第三方依赖**：`MemoDock.Core` 不引用任何 NuGet 包。
- **架构分层**：`MemoDock.Core`（平台无关逻辑）→ `MemoDock`（WPF 界面与 Windows 集成）。

## 开发环境

- Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## 构建与测试

```powershell
dotnet restore .\MemoDock.sln --configfile .\NuGet.Config
dotnet build .\MemoDock.sln --configuration Release --no-restore
dotnet run --project .\tests\MemoDock.CoreTests\MemoDock.CoreTests.csproj --configuration Release --no-restore
```

> 测试是自包含的控制台应用，不使用 xUnit/NUnit 等第三方框架，以保持零依赖。核心逻辑改动必须同步更新 `tests/MemoDock.CoreTests/Program.cs` 中的自测。

## 代码边界

- `src/MemoDock.Core/` 只放平台无关的数据模型、查询、迁移和本地存储。
  - 数据库结构迁移放在 `Services/MemoMigrator.cs`。
  - 路径常量集中在 `Services/AppPaths.cs`。
  - 原子写文件统一使用 `Services/AtomicFile.cs`。
- `src/MemoDock/` 放 WPF 界面、Win32 前台窗口识别、全局快捷键、系统托盘和窗口效果。
  - 上下文菜单统一由 `Services/ContextMenuBuilder.cs` 构建。

## 必须保持的产品行为

改动时请确保以下行为不回归：

- `Ctrl + Alt + N` 只显示并激活主窗口，不自动打开新建窗口。
- 单实例运行；重复启动只激活已有实例。
- 关闭按钮和 `Esc` 只隐藏到托盘；只有托盘"退出"才结束进程。
- 数据保存在 `%LOCALAPPDATA%\MemoDock\`，不静默上传或引入云端依赖。
- 覆盖数据前保留 `memos.json.bak`；主文件损坏时优先恢复备份。
- 普通软件以规范化后的完整 EXE 路径为主键；商店应用使用不含版本号的稳定身份。
- 主窗口保留顶部拖动和四边、四角缩放能力。

## 提 PR 的流程

1. Fork 本仓库并创建特性分支。
2. 保持改动聚焦：一个 PR 解决一个问题。
3. 确保 `dotnet build` 无警告、测试全部通过。
4. 描述你的改动动机和验证方式。
5. 等待 review 或直接联系维护者。

## 代码风格

- 跟随现有代码风格；命名、缩进、注释与周围保持一致。
- 公开 API 添加 XML 文档注释。
- 不提交 `bin/`、`obj/`、`artifacts/`、`.dotnet-cli/`、`.dotnet-sdk/`、`tmp/` 或 `*.log`。
- 不提交真实备忘录数据、用户路径转储、密钥或账户信息。
