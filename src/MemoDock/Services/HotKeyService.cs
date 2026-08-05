using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MemoDock.Services;

/// <summary>注册并监听全局快捷键（Ctrl + Alt + N）。</summary>
public sealed class HotKeyService : IDisposable
{
    private const int HotKeyId = 0x4D44;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VirtualKeyN = 0x4E;

    private HwndSource? _source;
    private IntPtr _windowHandle;
    private Action? _onPressed;

    /// <summary>全局快捷键是否注册成功（可能被其他程序占用）。</summary>
    public bool IsRegistered { get; private set; }

    /// <summary>
    /// 为指定窗口注册全局快捷键。
    /// </summary>
    /// <param name="window">接收热键消息的窗口。</param>
    /// <param name="onPressed">快捷键按下时的回调。</param>
    public void Register(Window window, Action onPressed)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(onPressed);

        _windowHandle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WindowHook);
        _onPressed = onPressed;

        IsRegistered = RegisterHotKey(
            _windowHandle,
            HotKeyId,
            ModControl | ModAlt | ModNoRepeat,
            VirtualKeyN);
    }

    /// <summary>注销快捷键并释放资源。</summary>
    public void Dispose()
    {
        if (IsRegistered)
        {
            _ = UnregisterHotKey(_windowHandle, HotKeyId);
            IsRegistered = false;
        }

        _source?.RemoveHook(WindowHook);
        _source = null;
        _onPressed = null;
    }

    private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            handled = true;
            _onPressed?.Invoke();
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
