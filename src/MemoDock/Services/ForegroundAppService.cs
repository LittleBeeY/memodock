using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MemoDock.Core.Models;
using MemoDock.Core.Services;

namespace MemoDock.Services;

/// <summary>识别当前前台窗口所属的软件，并提供其图标。</summary>
public sealed class ForegroundAppService
{
    /// <summary>
    /// 获取当前前台应用快照。
    /// </summary>
    /// <returns>前台应用快照；遇到桌面、本进程或无法识别的情况返回 <c>null</c>。</returns>
    public ForegroundAppSnapshot? TryGetForegroundApp()
    {
        var windowHandle = GetForegroundWindow();
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        if (IsWindowsShellSurface(windowHandle))
        {
            return null;
        }

        _ = GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0 || processId == Environment.ProcessId)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var executablePath = TryGetExecutablePath(process);
            var displayName = GetDisplayName(process, executablePath);
            var appId = AppIdentity.Create(executablePath, process.ProcessName);

            return new ForegroundAppSnapshot(
                new AppDescriptor(appId, displayName, executablePath),
                TryGetIcon(executablePath));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// 由已知的软件身份构建快照（用于手动切换）。
    /// </summary>
    public ForegroundAppSnapshot CreateSnapshot(AppDescriptor descriptor)
    {
        return new ForegroundAppSnapshot(descriptor, TryGetIcon(descriptor.ExecutablePath));
    }

    /// <summary>判断窗口是否为任务栏、桌面等系统外壳表面。</summary>
    private static bool IsWindowsShellSurface(IntPtr windowHandle)
    {
        var className = new StringBuilder(256);
        if (GetClassName(windowHandle, className, className.Capacity) == 0)
        {
            return false;
        }

        return className.ToString() is
            "Shell_TrayWnd" or
            "Shell_SecondaryTrayWnd" or
            "Progman" or
            "WorkerW";
    }

    /// <summary>尽力取得进程可执行文件路径；失败时返回空字符串。</summary>
    private static string TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
        catch (NotSupportedException)
        {
            return string.Empty;
        }
    }

    /// <summary>优先使用文件描述信息作为展示名，缺失时回退到进程名。</summary>
    private static string GetDisplayName(Process process, string executablePath)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                var description = FileVersionInfo.GetVersionInfo(executablePath).FileDescription;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    return description.Trim();
                }
            }
            catch (FileNotFoundException)
            {
            }
        }

        return process.ProcessName;
    }

    /// <summary>提取可执行文件的关联图标；失败时返回 <c>null</c>。</summary>
    private static ImageSource? TryGetIcon(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var icon = TryExtractAssociatedIcon(executablePath)
            ?? TryExtractShellIcon(executablePath);

        if (icon is null)
        {
            return null;
        }

        using (icon)
        {
            try
            {
                var image = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                return image;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (ExternalException)
            {
                return null;
            }
        }
    }

    /// <summary>通过文件关联直接提取图标。</summary>
    private static Icon? TryExtractAssociatedIcon(string executablePath)
    {
        try
        {
            return Icon.ExtractAssociatedIcon(executablePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ExternalException or
            FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// 通过 Shell API（SHGetFileInfo）提取图标；对商店应用、
    /// 浏览器等 ExtractAssociatedIcon 失败的情况更可靠。
    /// </summary>
    private static Icon? TryExtractShellIcon(string executablePath)
    {
        var info = new ShFileInfo();
        var handle = SHGetFileInfo(
            executablePath,
            0,
            ref info,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            ShgfiIcon | ShgfiLargeIcon);

        if (handle == IntPtr.Zero || info.HIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Icon.FromHandle(info.HIcon);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ExternalException)
        {
            return null;
        }
    }

    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct ShFileInfo
    {
        public IntPtr HIcon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maximumCount);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);
}
