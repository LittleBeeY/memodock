namespace MemoDock.Core.Models;

/// <summary>
/// 应用设置。使用字符串保存快捷键（与 WPF 类型解耦），
/// 由 UI 层负责解析与校验。
/// </summary>
public sealed class AppSettings
{
    /// <summary>全局快捷键的修饰键（如 "Control, Alt"）。</summary>
    public string HotKeyModifiers { get; set; } = "Control, Alt";

    /// <summary>全局快捷键的主键（如 "N"）。</summary>
    public string HotKeyKey { get; set; } = "N";

    /// <summary>是否随系统启动（开机自启）。</summary>
    public bool LaunchOnStartup { get; set; }
}
