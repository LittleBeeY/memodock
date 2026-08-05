using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MemoDock.Services;

/// <summary>应用 Windows 11 桌面效果：深色模式、圆角与 Acrylic 背景。</summary>
public static class WindowEffects
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmSystemBackdropType = 38;

    /// <summary>
    /// 为窗口应用 DWM 效果。需要窗口句柄已创建，否则静默跳过。
    /// </summary>
    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (HwndSource.FromHwnd(handle) is { CompositionTarget: not null } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);

        var darkMode = 1;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref darkMode, sizeof(int));

        var roundedCorners = 2;
        _ = DwmSetWindowAttribute(handle, DwmWindowCornerPreference, ref roundedCorners, sizeof(int));

        var desktopAcrylic = 3;
        _ = DwmSetWindowAttribute(handle, DwmSystemBackdropType, ref desktopAcrylic, sizeof(int));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr windowHandle, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
