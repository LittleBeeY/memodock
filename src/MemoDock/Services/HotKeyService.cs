using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MemoDock.Services;

/// <summary>注册并监听可配置的全局快捷键。</summary>
public sealed class HotKeyService : IDisposable
{
    private const int HotKeyId = 0x4D44;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private HwndSource? _source;
    private IntPtr _windowHandle;
    private Action? _onPressed;

    /// <summary>当前注册的快捷键组合文本（用于界面提示）。</summary>
    public string RegisteredComboText { get; private set; } = string.Empty;

    /// <summary>全局快捷键是否注册成功（可能被其他程序占用）。</summary>
    public bool IsRegistered { get; private set; }

    /// <summary>
    /// 为指定窗口注册全局快捷键；已注册的组合会自动注销后重注册。
    /// </summary>
    /// <param name="window">接收热键消息的窗口。</param>
    /// <param name="modifiers">修饰键组合。</param>
    /// <param name="key">主键。</param>
    /// <param name="onPressed">快捷键按下时的回调。</param>
    public void Register(Window window, ModifierKeys modifiers, Key key, Action onPressed)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(onPressed);

        // 重新注册前先注销旧的，避免重复占用。
        Unregister();

        _windowHandle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WindowHook);
        _onPressed = onPressed;

        var modifiersValue = ToModifiersValue(modifiers);
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

        IsRegistered = RegisterHotKey(
            _windowHandle,
            HotKeyId,
            modifiersValue | ModNoRepeat,
            virtualKey);

        RegisteredComboText = IsRegistered ? FormatCombo(modifiers, key) : string.Empty;
    }

    /// <summary>把快捷键组合转成可读文本（如 "Ctrl + Alt + N"）。</summary>
    public static string FormatCombo(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }

    /// <summary>注销快捷键并释放资源。</summary>
    public void Dispose()
    {
        Unregister();
    }

    private void Unregister()
    {
        if (IsRegistered)
        {
            _ = UnregisterHotKey(_windowHandle, HotKeyId);
            IsRegistered = false;
        }

        _source?.RemoveHook(WindowHook);
        _source = null;
        _onPressed = null;
        RegisteredComboText = string.Empty;
    }

    private static uint ToModifiersValue(ModifierKeys modifiers)
    {
        var value = 0u;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            value |= ModControl;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            value |= ModAlt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            value |= ModShift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            value |= ModWin;
        }

        return value;
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
