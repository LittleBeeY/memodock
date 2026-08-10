using System.Text.Json;
using System.Text.Json.Serialization;
using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

/// <summary>备忘录的加载、持久化与导出。记录以本地 JSON 文件保存，不依赖网络。</summary>
public sealed class MemoRepository
{
    /// <summary>保留的历史备份份数（<c>.bak</c> 与 <c>.bak.1</c>）。</summary>
    private const int BackupCount = 2;

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
    /// 主文件缺失（如保存中途崩溃）但存在备份时，同样从备份恢复。
    /// 加载后如有结构变化会自动迁移并写回。
    /// </summary>
    public void Load()
    {
        if (!File.Exists(_databasePath))
        {
            // 主文件不存在：首次启动（无备份）返回空库；若留下备份
            // （写入中途崩溃），则从备份恢复而不是误判为空库。
            Database = TryReadBackupChain() ?? new MemoDatabase();
            if (Database.Apps.Count > 0)
            {
                Save();
            }

            return;
        }

        try
        {
            Database = ReadDatabase(_databasePath);
        }
        catch (JsonException)
        {
            PreserveCorruptDatabase();
            CleanupCorruptBackups();
            Database = TryReadBackupChain() ?? new MemoDatabase();
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

    /// <summary>原子方式保存当前数据库；覆盖前保留多份滚动备份。</summary>
    public void Save()
    {
        var json = JsonSerializer.Serialize(Database, _jsonOptions);
        AtomicFile.WriteAllTextWithRollingBackup(_databasePath, json, BackupCount);
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
        // 用毫秒时间戳 + 随机后缀，避免同一时间窗口内多次损坏导致文件名冲突。
        var backupPath = $"{_databasePath}.corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        File.Move(_databasePath, backupPath, overwrite: false);
    }

    /// <summary>清理损坏现场备份，只保留最近 10 份，避免磁盘无限累积。</summary>
    private void CleanupCorruptBackups()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        var prefix = Path.GetFileName(_databasePath) + ".corrupt-";
        if (directory is null)
        {
            return;
        }

        var staleFiles = Directory.EnumerateFiles(directory, prefix + "*")
            .OrderByDescending(path => path)
            .Skip(10);
        foreach (var file in staleFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>读取并反序列化数据库文件；内容为 null 时返回空数据库。</summary>
    private MemoDatabase ReadDatabase(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MemoDatabase>(json, _jsonOptions) ?? new MemoDatabase();
    }

    /// <summary>
    /// 依次尝试最新与更早的滚动备份，返回第一个可解析的；全部失败时返回 <c>null</c>。
    /// </summary>
    private MemoDatabase? TryReadBackupChain()
    {
        for (var index = 0; index < BackupCount; index++)
        {
            var backupPath = index == 0 ? _databasePath + ".bak" : $"{_databasePath}.bak.{index}";
            if (!File.Exists(backupPath))
            {
                continue;
            }

            try
            {
                return ReadDatabase(backupPath);
            }
            catch (JsonException)
            {
                // 这一级备份也损坏，继续尝试更早的。
            }
        }

        return null;
    }
}
