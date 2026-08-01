<div align="center">
  <img src="./src/MemoDock/Assets/MemoDock.svg" width="96" alt="MemoDock 图标">
  <h1>MemoDock</h1>
  <p>为每个 Windows 软件准备一份独立的本地备忘录。</p>
  <p><strong>Windows 11 · WPF · .NET 10 · Local-first</strong></p>
</div>

MemoDock 会识别打开前最后处于前台的软件，并自动切换到该软件独立的笔记和待办。记录保存在本机，不需要账户或网络。

> 当前版本：`0.2.1` 可分享 Beta

<p align="center">
  <img src="./output/imagegen/memodock-sidebar-v5.png" width="360" alt="MemoDock 界面预览">
</p>

## 功能

- 按软件身份隔离记录；商店应用升级后仍能关联原有笔记
- 笔记和待办两种记录类型
- 当前软件内搜索、新建、编辑和删除
- 待办完成状态
- `Ctrl + Alt + N` 全局快捷键
- 单实例运行，重复启动只唤醒已有窗口
- 窗口拖动、缩放及大小和位置记忆
- 关闭到系统托盘，托盘菜单可导出数据备份
- Windows 11 Desktop Acrylic、圆角和深色界面
- 覆盖前自动保留上一版数据，主文件损坏时尝试恢复
- 无账户、无遥测、无云端依赖

## 快速开始

### 运行源码

需要：

- Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
dotnet run --project .\src\MemoDock\MemoDock.csproj
```

### 使用方式

1. 保持 MemoDock 在后台运行。
2. 切换到需要记录的软件。
3. 按 `Ctrl + Alt + N`，或双击托盘图标打开 MemoDock。
4. 选择“笔记”或“待办”，添加当前软件的记录。
5. 关闭按钮和 `Esc` 只会隐藏窗口；托盘菜单“退出”才会结束程序。
6. 需要额外备份时，在托盘菜单选择“导出数据备份…”。

快捷键只负责显示窗口，不会自动打开新建记录窗口。

## 数据与隐私

MemoDock 不会上传记录，也不依赖网络服务。数据默认保存在：

| 文件 | 用途 |
| --- | --- |
| `%LOCALAPPDATA%\MemoDock\memos.json` | 当前笔记和待办 |
| `%LOCALAPPDATA%\MemoDock\memos.json.bak` | 覆盖前的上一版数据 |
| `%LOCALAPPDATA%\MemoDock\window.json` | 窗口大小和位置 |

保存时会先写临时文件，再替换正式文件。如果主 JSON 损坏，原文件会改名保留，并自动尝试恢复 `.bak`。
旧版数据会在加载时自动迁移：普通桌面程序仍按完整 EXE 路径区分，Windows 商店应用使用不含版本号的稳定身份，并自动合并升级前后的记录。迁移覆盖前同样会生成 `.bak`。

> “本地私有”表示数据不离开当前电脑；记录目前以明文 JSON 保存，不等同于加密存储。

## 开发

### 构建与测试

```powershell
dotnet restore .\MemoDock.sln --configfile .\NuGet.Config
dotnet build .\MemoDock.sln --configuration Release --no-restore
dotnet run --project .\tests\MemoDock.CoreTests\MemoDock.CoreTests.csproj --configuration Release --no-restore
```

当前自测覆盖：

- 不同软件的记录隔离和持久化
- 标题和正文搜索
- 损坏数据恢复上一版
- 保存时保留上一版备份
- 导出数据副本
- 商店应用升级后的稳定识别和旧记录迁移

### 发布

默认发布是依赖框架的多文件版本，分享时必须发送整个发布目录：

```powershell
dotnet publish .\src\MemoDock\MemoDock.csproj `
  --configuration Release `
  --no-restore `
  --output .\artifacts\MemoDock
```

面向普通 Windows x64 用户时，建议生成无需预装 .NET 的自包含单文件版：

```powershell
dotnet publish .\src\MemoDock\MemoDock.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  --source https://api.nuget.org/v3/index.json `
  --output .\artifacts\MemoDock-0.2.1-win-x64
```

仓库的 `NuGet.Config` 默认不配置远程包源，上面的 `--source` 只用于获取官方 .NET 自包含运行时包。单文件版本与处理器架构绑定；ARM64 设备需要改用 `win-arm64` 单独发布。

## 工程结构

```text
src/
  MemoDock.Core/       数据模型、搜索和本地存储
  MemoDock/            WPF 界面与 Windows 集成
    Assets/            应用图标源文件和 ICO
    Services/          前台识别、快捷键、单实例和窗口效果
tests/
  MemoDock.CoreTests/  无第三方测试框架的核心逻辑自测
output/imagegen/       已确认的界面视觉基准
```

## 当前限制

- 同一程序的不同工作区暂时共用一份记录。
- 少数使用系统进程包装器、且无法取得实际 EXE 路径的应用可能显示不准确。
- 首次打开停靠到主工作区右侧，尚未跟随前台软件所在显示器。
- 全局快捷键固定为 `Ctrl + Alt + N`，暂无设置界面。
- 尚未制作 MSIX 安装包、代码签名和开机启动设置。
