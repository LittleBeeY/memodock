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
            var json = File.ReadAllText(_databasePath);
            Database = JsonSerializer.Deserialize<MemoDatabase>(json, _jsonOptions) ?? new MemoDatabase();
        }
        catch (JsonException)
        {
            PreserveCorruptDatabase();
            Database = new MemoDatabase();
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
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _databasePath, overwrite: true);
    }

    private void PreserveCorruptDatabase()
    {
        var backupPath = $"{_databasePath}.corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        File.Move(_databasePath, backupPath, overwrite: false);
    }

    private static string GetDefaultDatabasePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "MemoDock", "memos.json");
    }
}
