using System.IO;
using System.Text.Json;
using System.Windows;
using MemoDock.Core.Services;

namespace MemoDock.Services;

/// <summary>主窗口大小与位置的读取与持久化。</summary>
public sealed class WindowStateService
{
    private readonly string _settingsPath;
    private const double MinVisibleExtent = 80;

    public WindowStateService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? AppPaths.WindowStatePath;
    }

    /// <summary>
    /// 尝试恢复窗口上次的大小和位置。
    /// </summary>
    /// <param name="window">要恢复的目标窗口。</param>
    /// <returns>是否成功恢复；文件缺失、损坏或坐标失效时返回 <c>false</c>。</returns>
    public bool TryRestore(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        try
        {
            if (!File.Exists(_settingsPath))
            {
                return false;
            }

            var json = File.ReadAllText(_settingsPath);
            var state = JsonSerializer.Deserialize<WindowStateData>(json);
            if (state is null || !IsVisible(state, window))
            {
                return false;
            }

            window.Width = state.Width;
            window.Height = state.Height;
            window.Left = state.Left;
            window.Top = state.Top;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException)
        {
            return false;
        }
    }

    /// <summary>保存窗口当前的大小和位置；窗口非普通状态时跳过。</summary>
    public void Save(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.WindowState != WindowState.Normal)
        {
            return;
        }

        try
        {
            var state = new WindowStateData(
                window.Left,
                window.Top,
                window.Width,
                window.Height);
            var json = JsonSerializer.Serialize(state);
            AtomicFile.WriteAllText(_settingsPath, json, keepBackup: false);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            // 窗口位置保存失败不影响应用运行，静默忽略。
        }
    }

    /// <summary>检查记录的坐标在当前虚拟屏幕中仍有可见区域。</summary>
    private static bool IsVisible(WindowStateData state, Window window)
    {
        if (!double.IsFinite(state.Left) ||
            !double.IsFinite(state.Top) ||
            !double.IsFinite(state.Width) ||
            !double.IsFinite(state.Height) ||
            state.Width < window.MinWidth ||
            state.Height < window.MinHeight ||
            state.Width > SystemParameters.VirtualScreenWidth ||
            state.Height > SystemParameters.VirtualScreenHeight)
        {
            return false;
        }

        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        var visibleWidth = Math.Min(state.Left + state.Width, virtualRight) - Math.Max(state.Left, virtualLeft);
        var visibleHeight = Math.Min(state.Top + state.Height, virtualBottom) - Math.Max(state.Top, virtualTop);
        return visibleWidth >= MinVisibleExtent && visibleHeight >= MinVisibleExtent;
    }

    private sealed record WindowStateData(double Left, double Top, double Width, double Height);
}
