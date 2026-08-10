using System.IO;
using System.Text.Json;
using MemoDock.Core.Models;

namespace MemoDock.Core.Services;

/// <summary>应用设置的读取与持久化。</summary>
public sealed class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? AppPaths.SettingsPath;
        Current = Load();
    }

    /// <summary>当前设置；文件缺失或损坏时回退默认值。</summary>
    public AppSettings Current { get; private set; }

    /// <summary>保存设置（原子写）。</summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Current = settings;
        try
        {
            var json = JsonSerializer.Serialize(settings);
            AtomicFile.WriteAllText(_settingsPath, json, keepBackup: false);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            // 设置保存失败不阻断运行，回退为当前内存值。
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return new AppSettings();
        }
    }
}
