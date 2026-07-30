using System.IO;
using System.Text.Json;
using System.Windows;

namespace MemoDock.Services;

public sealed class WindowStateService
{
    private readonly string _settingsPath;

    public WindowStateService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

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

    public void Save(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.WindowState != WindowState.Normal)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)
                ?? throw new InvalidOperationException("窗口设置路径缺少目录。");
            Directory.CreateDirectory(directory);

            var state = new WindowStateData(
                window.Left,
                window.Top,
                window.Width,
                window.Height);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
        }
    }

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
        return visibleWidth >= 80 && visibleHeight >= 80;
    }

    private static string GetDefaultSettingsPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "MemoDock", "window.json");
    }

    private sealed record WindowStateData(double Left, double Top, double Width, double Height);
}
