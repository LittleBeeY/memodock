using System.Text.Json;
using System.Text.Json.Serialization;
using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

/// <summary>备忘录的加载、持久化与导出。记录以本地 JSON 文件保存，不依赖网络。</summary>
public sealed class MemoRepository
{
    private readonly string _databasePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 创建一个仓库实例。
    /// </summary>
    /// <param name="databasePath">数据库文件路径；为 <c>null</c> 时使用默认位置。</param>
    public MemoRepository(string? databasePath = null)
    {
        _databasePath = databasePath ?? AppPaths.DatabasePath;
    }

    /// <summary>当前内存中的数据库。</summary>
    public MemoDatabase Database { get; private set; } = new();

    /// <summary>数据库文件完整路径。</summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// 从磁盘加载数据库；主文件损坏时保留原文件并尝试恢复上一版备份。
    /// 加载后如有结构变化会自动迁移并写回。
    /// </summary>
    public void Load()
    {
        if (!File.Exists(_databasePath))
        {
            Database = new MemoDatabase();
            return;
        }

        try
        {
            Database = ReadDatabase(_databasePath);
        }
        catch (JsonException)
        {
            PreserveCorruptDatabase();
            Database = TryReadBackup() ?? new MemoDatabase();
        }

        if (MemoMigrator.Migrate(Database))
        {
            Save();
        }
    }

    /// <summary>
    /// 获取（必要时创建）某个软件的记录本。
    /// </summary>
    /// <param name="app">软件身份描述。</param>
    public AppNotebook GetOrCreateNotebook(AppDescriptor app)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app.AppId);

        var notebook = Database.Apps.FirstOrDefault(
            item => string.Equals(item.AppId, app.AppId, StringComparison.OrdinalIgnoreCase));

        if (notebook is null)
        {
            notebook = new AppNotebook
            {
                AppId = app.AppId,
                DisplayName = app.DisplayName,
                ExecutablePath = app.ExecutablePath
            };
            Database.Apps.Add(notebook);
        }
        else
        {
            notebook.DisplayName = app.DisplayName;
            notebook.ExecutablePath = app.ExecutablePath;
        }

        return notebook;
    }

    /// <summary>原子方式保存当前数据库；覆盖前自动保留上一版备份。</summary>
    public void Save()
    {
        var json = JsonSerializer.Serialize(Database, _jsonOptions);
        AtomicFile.WriteAllText(_databasePath, json, keepBackup: true);
    }

    /// <summary>
    /// 把当前数据库导出为独立的 JSON 副本（用于用户备份）。
    /// </summary>
    /// <param name="destinationPath">导出目标文件路径。</param>
    public void ExportTo(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fullPath = Path.GetFullPath(destinationPath);
        var json = JsonSerializer.Serialize(Database, _jsonOptions);
        AtomicFile.WriteAllText(fullPath, json, keepBackup: false);
    }

    /// <summary>把无法解析的损坏文件改名保留，避免覆盖用户数据。</summary>
    private void PreserveCorruptDatabase()
    {
        var backupPath = $"{_databasePath}.corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        File.Move(_databasePath, backupPath, overwrite: false);
    }

    /// <summary>读取并反序列化数据库文件；内容为 null 时返回空数据库。</summary>
    private MemoDatabase ReadDatabase(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MemoDatabase>(json, _jsonOptions) ?? new MemoDatabase();
    }

    /// <summary>尝试读取上一版备份；备份不存在或损坏时返回 <c>null</c>。</summary>
    private MemoDatabase? TryReadBackup()
    {
        var backupPath = _databasePath + ".bak";
        if (!File.Exists(backupPath))
        {
            return null;
        }

        try
        {
            return ReadDatabase(backupPath);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
