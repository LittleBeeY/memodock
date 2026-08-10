using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MemoDock.Core.Models;
using MemoDock.Core.Services;

namespace MemoDock.Services;

/// <summary>识别当前前台窗口所属的软件，并提供其图标。</summary>
public sealed class ForegroundAppService : IDisposable
{
    /// <summary>图标缓存上限；超过时清空重建，防止长期运行累积。</summary>
    private const int IconCacheLimit = 512;

    private const uint EventSystemForeground = 0x0003;
    private const uint WineventOutofcontext = 0x0000;

    private readonly Dictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private IntPtr _lastForegroundHandle;
    private ForegroundAppSnapshot? _lastSnapshot;
    private WinEventDelegate? _winEventHookDelegate;
    private IntPtr _winEventHook;
    private Dispatcher? _dispatcher;

    /// <summary>
    /// 获取当前前台应用快照。前台窗口未变化时直接返回上次结果，
    /// 避免反复读取 exe 元数据与提取图标。
    /// </summary>
    /// <returns>前台应用快照；遇到桌面、本进程或无法识别的情况返回 <c>null</c>。</returns>
    public ForegroundAppSnapshot? TryGetForegroundApp()
    {
        var windowHandle = GetForegroundWindow();
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        if (windowHandle == _lastForegroundHandle)
        {
            return _lastSnapshot;
        }

        var snapshot = TryBuildSnapshot(windowHandle);
        _lastForegroundHandle = windowHandle;
        _lastSnapshot = snapshot;
        return snapshot;
    }

    /// <summary>
    /// 由已知的软件身份构建快照（用于手动切换）。
    /// </summary>
    public ForegroundAppSnapshot CreateSnapshot(AppDescriptor descriptor)
    {
        return new ForegroundAppSnapshot(descriptor, TryGetIcon(descriptor.ExecutablePath));
    }

    /// <summary>当前前台窗口句柄；获取失败时返回 <see cref="IntPtr.Zero"/>。</summary>
    public IntPtr TryGetForegroundWindowHandle()
    {
        return GetForegroundWindow();
    }

    /// <summary>前台软件切换时触发（事件驱动，替代高频轮询）。</summary>
    public event EventHandler? ForegroundChanged;

    /// <summary>
    /// 开始监听前台窗口切换事件。需在 UI 线程（有消息泵）调用；
    /// 回调会封送到该线程。
    /// </summary>
    public void StartListening()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            return;
        }

        _dispatcher = Dispatcher.CurrentDispatcher;
        _winEventHookDelegate = HandleForegroundEvent;
        _winEventHook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            _winEventHookDelegate,
            0,
            0,
            WineventOutofcontext);
    }

    /// <summary>停止监听前台窗口切换事件。</summary>
    public void StopListening()
    {
        if (_winEventHook != IntPtr.Zero)
        {
            _ = UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }

        _winEventHookDelegate = null;
        _dispatcher = null;
    }

    public void Dispose()
    {
        StopListening();
    }

    /// <summary>前台切换事件的回调：只封送通知，不在此处做任何提取工作。</summary>
    private void HandleForegroundEvent(
        IntPtr hookHandle,
        uint eventType,
        IntPtr windowHandle,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime)
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        dispatcher.BeginInvoke(() => ForegroundChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>根据前台窗口句柄识别软件；任何失败都降级为 <c>null</c>，不让进程崩溃。</summary>
    private ForegroundAppSnapshot? TryBuildSnapshot(IntPtr windowHandle)
    {
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
        catch (Exception)
        {
            // 前台识别是尽力而为：识别失败不应影响应用运行。
            return null;
        }
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

    /// <summary>提取可执行文件的关联图标（带缓存）；失败时返回 <c>null</c>。</summary>
    private ImageSource? TryGetIcon(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        if (_iconCache.TryGetValue(executablePath, out var cached))
        {
            return cached;
        }

        ImageSource? image;
        try
        {
            // 图标提取是尽力而为：路径可能来自磁盘存储（不可信），
            // 任何提取失败都应降级为无图标，而不是让界面崩溃。
            image = TryGetAssociatedIconImage(executablePath)
                ?? TryGetShellIconImage(executablePath);
        }
        catch (Exception)
        {
            return null;
        }

        if (image is not null)
        {
            if (_iconCache.Count >= IconCacheLimit)
            {
                // 缓存超限时清空重建，避免长期运行无限增长。
                _iconCache.Clear();
            }

            _iconCache[executablePath] = image;
        }

        return image;
    }

    /// <summary>通过文件关联直接提取图标并转为位图源。</summary>
    private static ImageSource? TryGetAssociatedIconImage(string executablePath)
    {
        Icon? icon = null;
        try
        {
            icon = Icon.ExtractAssociatedIcon(executablePath);
            return icon is null ? null : CreateImageSource(icon.Handle);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ExternalException or
            FileNotFoundException)
        {
            return null;
        }
        finally
        {
            icon?.Dispose();
        }
    }

    /// <summary>
    /// 通过 Shell API（SHGetFileInfo）提取图标并转为位图源；对商店应用、
    /// 浏览器等 ExtractAssociatedIcon 失败的情况更可靠。
    /// 返回的 HICON 句柄在转换后立即释放，避免句柄泄漏。
    /// </summary>
    private static ImageSource? TryGetShellIconImage(string executablePath)
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
            return CreateImageSource(info.HIcon);
        }
        finally
        {
            DestroyIcon(info.HIcon);
        }
    }

    private static ImageSource? CreateImageSource(IntPtr iconHandle)
    {
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref ShFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr moduleHandle,
        WinEventDelegate callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hookHandle);

    private delegate void WinEventDelegate(
        IntPtr hookHandle,
        uint eventType,
        IntPtr windowHandle,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime);
}
