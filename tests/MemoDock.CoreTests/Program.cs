using MemoDock.Core.Models;
using MemoDock.Core.Services;

var failures = new List<string>();
Run("按软件隔离并持久化", RoundTripPersistsPerApp, failures);
Run("搜索标题和正文", SearchMatchesTitleAndBody, failures);
Run("损坏数据保留备份", CorruptDatabaseIsPreserved, failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine($"失败：{failures.Count} 项");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }

    return 1;
}

Console.WriteLine("MemoDock.CoreTests：3 项全部通过。");
return 0;

static void RoundTripPersistsPerApp()
{
    using var sandbox = new TestDirectory();
    var path = Path.Combine(sandbox.Path, "memos.json");
    var repository = new MemoRepository(path);

    var editor = repository.GetOrCreateNotebook(new AppDescriptor("editor.exe", "代码编辑器", "C:\\Apps\\editor.exe"));
    editor.Entries.Add(new MemoEntry { Kind = MemoKind.Note, Title = "发布前检查", Body = "更新版本说明" });

    var browser = repository.GetOrCreateNotebook(new AppDescriptor("browser.exe", "浏览器", "C:\\Apps\\browser.exe"));
    browser.Entries.Add(new MemoEntry { Kind = MemoKind.Todo, Title = "检查标签页" });
    repository.Save();

    var reloaded = new MemoRepository(path);
    reloaded.Load();

    Assert(reloaded.Database.Apps.Count == 2, "应恢复两个软件的独立记录。");
    Assert(reloaded.Database.Apps.Single(app => app.AppId == "editor.exe").Entries.Single().Title == "发布前检查", "编辑器笔记未恢复。");
    Assert(reloaded.Database.Apps.Single(app => app.AppId == "browser.exe").Entries.Single().Kind == MemoKind.Todo, "浏览器待办未恢复。");
}

static void SearchMatchesTitleAndBody()
{
    var entries = new[]
    {
        new MemoEntry { Kind = MemoKind.Note, Title = "常用快捷键", Body = "打开命令面板" },
        new MemoEntry { Kind = MemoKind.Note, Title = "版本", Body = "发布前检查" },
        new MemoEntry { Kind = MemoKind.Todo, Title = "命令面板" }
    };

    Assert(MemoQuery.Filter(entries, MemoKind.Note, "快捷键").Count() == 1, "标题搜索结果不正确。");
    Assert(MemoQuery.Filter(entries, MemoKind.Note, "发布").Count() == 1, "正文搜索结果不正确。");
    Assert(MemoQuery.Filter(entries, MemoKind.Note, "命令").Count() == 1, "搜索不应混入待办类型。");
    Assert(MemoQuery.Filter(entries, MemoKind.Note, " ").Count() == 2, "空白搜索应返回当前类型全部记录。");
}

static void CorruptDatabaseIsPreserved()
{
    using var sandbox = new TestDirectory();
    var path = Path.Combine(sandbox.Path, "memos.json");
    File.WriteAllText(path, "{not-json");

    var repository = new MemoRepository(path);
    repository.Load();

    Assert(repository.Database.Apps.Count == 0, "损坏数据后应使用空数据库继续运行。");
    Assert(Directory.GetFiles(sandbox.Path, "memos.json.corrupt-*").Length == 1, "损坏的原文件应保留为备份。");
}

static void Run(string name, Action test, ICollection<string> failures)
{
    try
    {
        test();
        Console.WriteLine($"通过：{name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name} — {exception.Message}");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"MemoDock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
