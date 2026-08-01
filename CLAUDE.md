# MemoDock 项目规则

MemoDock 是 Windows 11 原生 WPF 应用，目标框架为 .NET 10。它按前台程序的可执行文件路径隔离本地笔记和待办，不使用账户或网络服务。

## 必须保持的产品行为

- `Ctrl + Alt + N` 只显示并激活主窗口，不能自动打开新建记录窗口。
- 应用必须保持单实例；重复启动只激活已有实例。
- 主窗口关闭按钮和 `Esc` 只隐藏到系统托盘；只有托盘菜单“退出”才结束进程。
- 记录默认保存在 `%LOCALAPPDATA%\MemoDock\memos.json`，不得静默上传或引入云端依赖。
- 覆盖数据前保留 `memos.json.bak`，主文件损坏时优先恢复该备份。
- 普通桌面软件以规范化后的可执行文件完整路径为主键；Windows 商店应用使用不含版本号的包身份与包内路径，无法读取路径时才回退到进程名。
- 加载旧数据库时自动迁移并合并同一商店应用不同版本的记录，覆盖前必须保留 `memos.json.bak`。
- 主窗口必须保留顶部拖动和四边、四角缩放能力。
- 隐藏窗口时保存大小和位置；坐标失效时回退到默认停靠。

## 代码边界

- `src/MemoDock.Core/` 只放平台无关的数据模型、查询和本地存储逻辑。
- `src/MemoDock/` 放 WPF 界面、Win32 前台窗口识别、全局快捷键、系统托盘和窗口效果。
- 核心逻辑变更应同步更新 `tests/MemoDock.CoreTests/Program.cs` 中的无第三方框架自测。
- 视觉调整以 `output/imagegen/memodock-sidebar-v5.png` 为基准；应用图标源文件在 `src/MemoDock/Assets/`，不要提交其他临时生成图。

## 验证命令

```powershell
dotnet restore .\MemoDock.sln --configfile .\NuGet.Config
dotnet build .\MemoDock.sln --configuration Release --no-restore
dotnet run --project .\tests\MemoDock.CoreTests\MemoDock.CoreTests.csproj --configuration Release --no-restore
```

修改发布方式时同时核对 README 的“发布与分享”。默认发布目录是依赖 .NET 10 Desktop Runtime 的多文件版本；只有显式指定目标运行时、自包含和单文件参数后，才能把一个 EXE 作为完整分发物。`NuGet.Config` 有意清空远程包源，自包含发布时需显式使用官方 NuGet 源获取运行时包。

## 仓库卫生

- 不提交 `bin/`、`obj/`、`artifacts/`、`.dotnet-cli/`、`.dotnet-sdk/` 或 `tmp/`。
- 不提交真实备忘录数据、用户路径转储、密钥或账户信息。
- 只做与当前任务直接相关的修改，并保持现有代码风格。
