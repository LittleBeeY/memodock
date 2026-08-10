namespace MemoDock.Core.Services;

/// <summary>集中管理本地数据目录与文件名，避免各处重复拼接路径。</summary>
public static class AppPaths
{
    /// <summary>应用数据目录名。</summary>
    public const string AppDirectoryName = "MemoDock";

    /// <summary>备忘录数据库文件名。</summary>
    public const string DatabaseFileName = "memos.json";

    /// <summary>窗口状态文件名。</summary>
    public const string WindowStateFileName = "window.json";

    /// <summary>应用设置文件名。</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>本地应用数据根目录。</summary>
    public static string LocalAppDataRoot =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>MemoDock 专属数据目录。</summary>
    public static string AppDataDirectory => Path.Combine(LocalAppDataRoot, AppDirectoryName);

    /// <summary>备忘录数据库完整路径。</summary>
    public static string DatabasePath => Path.Combine(AppDataDirectory, DatabaseFileName);

    /// <summary>窗口状态文件完整路径。</summary>
    public static string WindowStatePath => Path.Combine(AppDataDirectory, WindowStateFileName);

    /// <summary>应用设置文件完整路径。</summary>
    public static string SettingsPath => Path.Combine(AppDataDirectory, SettingsFileName);
}
