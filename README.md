# MemoDock

MemoDock 是一个 Windows 11 本地优先的“每个软件一份私有备忘录”。它会识别当前前台软件，并自动切换到该软件独立的笔记和待办。

当前版本是可运行的原生 MVP，视觉以 `output/imagegen/memodock-sidebar-v5.png` 为基准。

## 已实现

- 自动识别最近使用的前台软件，并按可执行文件路径隔离记录
- 笔记 / 待办两种记录
- 当前软件内搜索
- 新建、编辑、删除记录
- 待办完成状态
- `Ctrl + Alt + N` 全局快捷键
- 可从窗口四边和四角调节大小，并可拖动顶部区域移动
- 关闭到系统托盘，双击托盘图标重新显示
- Windows 11 Desktop Acrylic、圆角和深色模式
- 本地 JSON 原子保存，不需要账户或网络
- 损坏数据自动保留为带时间戳的备份

## 运行

需要 Windows 11 与 .NET 9 Desktop Runtime。

```powershell
dotnet run --project .\src\MemoDock\MemoDock.csproj
```

也可以直接运行发布目录中的 `MemoDock.exe`。

## 构建与验证

```powershell
dotnet restore .\MemoDock.sln --configfile .\NuGet.Config
dotnet build .\MemoDock.sln --configuration Release --no-restore
dotnet run --project .\tests\MemoDock.CoreTests\MemoDock.CoreTests.csproj --configuration Release --no-restore
dotnet publish .\src\MemoDock\MemoDock.csproj --configuration Release --no-restore --output .\artifacts\MemoDock
```

## 数据与隐私

记录默认保存在：

```text
%LOCALAPPDATA%\MemoDock\memos.json
```

数据不会上传，也不依赖云服务。保存时先写临时文件，再替换正式文件，尽量避免异常退出导致文件只写入一半。

## 使用方式

1. 保持 MemoDock 在后台运行。
2. 切换到需要记录的软件。
3. 按 `Ctrl + Alt + N`，或从托盘打开 MemoDock；快捷键只显示窗口，不会自动新建记录。
4. 选择“笔记”或“待办”，添加只属于当前软件的记录。
5. 点击右上角关闭按钮会隐藏到托盘；在托盘菜单中选择“退出”才会结束程序。
6. 拖动窗口顶部可以移动位置；拖动四边或四角可以调整窗口大小。

## 工程结构

```text
src/
  MemoDock.Core/       数据模型、搜索和本地存储
  MemoDock/            WPF 界面、前台软件识别、托盘和全局快捷键
tests/
  MemoDock.CoreTests/  无第三方测试框架的核心逻辑自测
output/imagegen/       已确认的视觉示意图
```

## 当前限制

- 当前按可执行文件路径区分软件，同一程序的不同工作区暂时共用一份记录。
- 使用系统进程包装器的少数商店应用，显示名称可能不够准确。
- 首版停靠到主工作区右侧，尚未按前台软件所在显示器自动选择屏幕。
- 当前提供免安装发布目录，尚未制作 MSIX 安装包和开机启动设置。
