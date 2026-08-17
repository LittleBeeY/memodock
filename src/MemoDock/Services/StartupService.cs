using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MemoDock.Services;

/// <summary>
/// 开机自启管理：在 HKCU 的 Run 键下写入/移除本程序入口。
/// 仅作用于当前用户，无需管理员权限。
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MemoDock";

    private const uint KeyQueryValue = 0x0001;
    private const uint KeySetValue = 0x0002;
    private const uint RegSz = 1;

    private static readonly IntPtr HKeyCurrentUser = new(0x80000001);

    /// <summary>
    /// 是否已注册开机自启。
    /// 注册值存在但指向其他可执行文件（例如旧发布目录已被删除或替换）时视为未启用，
    /// 以便调用方在启动时把注册值更正为当前运行的程序。
    /// </summary>
    public static bool IsEnabled()
    {
        using var key = OpenKey(KeyQueryValue);
        if (key is null)
        {
            return false;
        }

        var commandLine = QueryRunValue(key.Value);
        if (commandLine is null)
        {
            return false;
        }

        var currentPath = Environment.ProcessPath;
        if (currentPath is null)
        {
            // 拿不到当前路径时退回旧行为：注册值存在即视为启用。
            return true;
        }

        return string.Equals(
            ExtractExecutablePath(commandLine),
            currentPath,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>读取 Run 值的完整命令行；不存在或读取失败时返回 <c>null</c>。</summary>
    private static string? QueryRunValue(IntPtr key)
    {
        var bufferSize = 0u;
        var result = RegQueryValueEx(
            key,
            ValueName,
            IntPtr.Zero,
            out _,
            null,
            ref bufferSize);
        if (result != 0 || bufferSize == 0)
        {
            return null;
        }

        var buffer = new byte[bufferSize];
        result = RegQueryValueEx(
            key,
            ValueName,
            IntPtr.Zero,
            out _,
            buffer,
            ref bufferSize);
        if (result != 0)
        {
            return null;
        }

        // REG_SZ 数据包含终止符，写入时已按字符数（含终止符）编码。
        return Encoding.Unicode.GetString(buffer, 0, (int)bufferSize).TrimEnd('\0');
    }

    /// <summary>
    /// 从 Run 值命令行中提取可执行文件路径，规则与 CreateProcess 一致：
    /// 首字符为引号时取一对引号内的内容，否则取第一个空格前的片段。
    /// </summary>
    private static string ExtractExecutablePath(string commandLine)
    {
        var text = commandLine.Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (text[0] == '"')
        {
            var closingQuote = text.IndexOf('"', 1);
            return closingQuote > 1 ? text[1..closingQuote] : text[1..];
        }

        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    /// <summary>设置或取消开机自启；注册表不可写时返回 <c>false</c>。</summary>
    public static bool SetEnabled(bool enabled)
    {
        using var key = OpenKey(KeyQueryValue | KeySetValue);
        if (key is null)
        {
            return false;
        }

        try
        {
            if (!enabled)
            {
                return RegDeleteValue(key.Value, ValueName) == 0;
            }

            // 自包含单文件模式下 Assembly.Location 恒为空，故不用它；
            // Environment.ProcessPath 在单文件下也返回 exe 实际路径。
            var executablePath = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, Path.GetFileName(Environment.GetCommandLineArgs()[0]));
            var commandLine = $"\"{executablePath}\"";
            // REG_SZ 要求以 null 结尾，且字节数须包含终止符，否则写入返回 ERROR_INVALID_PARAMETER。
            var data = Encoding.Unicode.GetBytes(commandLine + "\0");
            return RegSetValueEx(
                key.Value,
                ValueName,
                0,
                RegSz,
                data,
                (uint)data.Length) == 0;
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or
            System.Security.SecurityException)
        {
            return false;
        }
    }

    private static SafeRegistryKey? OpenKey(uint access)
    {
        var key = IntPtr.Zero;
        var result = RegOpenKeyEx(HKeyCurrentUser, RunKeyPath, 0, access, out key);
        if (result != 0)
        {
            return null;
        }

        return new SafeRegistryKey(key);
    }

    private sealed class SafeRegistryKey : IDisposable
    {
        public SafeRegistryKey(IntPtr value)
        {
            Value = value;
        }

        public IntPtr Value { get; }

        public void Dispose()
        {
            _ = RegCloseKey(Value);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegOpenKeyEx(
        IntPtr key,
        string subKey,
        uint options,
        uint access,
        out IntPtr openedKey);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegSetValueEx(
        IntPtr key,
        string valueName,
        uint reserved,
        uint type,
        byte[] data,
        uint dataSize);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegQueryValueEx(
        IntPtr key,
        string valueName,
        IntPtr reserved,
        out uint type,
        byte[]? data,
        ref uint dataSize);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegDeleteValue(IntPtr key, string valueName);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr key);
}
