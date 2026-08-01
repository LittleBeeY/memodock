using System.Text.Json;
using System.Text.Json.Serialization;
using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

public sealed class MemoRepository
{
    private readonly string _databasePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MemoRepository(string? databasePath = null)
    {
        _databasePath = databasePath ?? GetDefaultDatabasePath();
    }

    public MemoDatabase Database { get; private set; } = new();

    public string DatabasePath => _databasePath;

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

        if (MigrateLegacyAppIdentities())
        {
            Save();
        }
    }

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

    public void Save()
    {
        var directory = Path.GetDirectoryName(_databasePath)
            ?? throw new InvalidOperationException("数据库路径缺少目录。");

        Directory.CreateDirectory(directory);

        var temporaryPath = _databasePath + ".tmp";
        var json = JsonSerializer.Serialize(Database, _jsonOptions);
        try
        {
            File.WriteAllText(temporaryPath, json);
            if (File.Exists(_databasePath))
            {
                File.Replace(temporaryPath, _databasePath, _databasePath + ".bak");
            }
            else
            {
                File.Move(temporaryPath, _databasePath);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public void ExportTo(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("导出路径缺少目录。");

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(Database, _jsonOptions);
        File.WriteAllText(fullPath, json);
    }

    private void PreserveCorruptDatabase()
    {
        var backupPath = $"{_databasePath}.corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        File.Move(_databasePath, backupPath, overwrite: false);
    }

    private MemoDatabase ReadDatabase(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MemoDatabase>(json, _jsonOptions) ?? new MemoDatabase();
    }

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

    private bool MigrateLegacyAppIdentities()
    {
        var changed = Database.Version < 2;
        Database.Version = 2;

        foreach (var notebook in Database.Apps)
        {
            if (!AppIdentity.TryCreatePackagedAppId(notebook.ExecutablePath, out var stableAppId) ||
                string.Equals(notebook.AppId, stableAppId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            notebook.AppId = stableAppId;
            changed = true;
        }

        foreach (var group in Database.Apps
                     .GroupBy(notebook => notebook.AppId, StringComparer.OrdinalIgnoreCase)
                     .Where(group =>
                         group.Key.StartsWith("windows-package:", StringComparison.OrdinalIgnoreCase) &&
                         group.Count() > 1)
                     .ToList())
        {
            var target = group.First();
            foreach (var source in group.Skip(1))
            {
                MergeEntries(target, source);
                Database.Apps.Remove(source);
            }

            changed = true;
        }

        return changed;
    }

    private static void MergeEntries(AppNotebook target, AppNotebook source)
    {
        foreach (var sourceEntry in source.Entries)
        {
            var existing = target.Entries.FirstOrDefault(entry => entry.Id == sourceEntry.Id);
            if (existing is null)
            {
                target.Entries.Add(sourceEntry);
                continue;
            }

            if (sourceEntry.UpdatedAt <= existing.UpdatedAt)
            {
                continue;
            }

            var index = target.Entries.IndexOf(existing);
            target.Entries[index] = sourceEntry;
        }
    }

    private static string GetDefaultDatabasePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "MemoDock", "memos.json");
    }
}
