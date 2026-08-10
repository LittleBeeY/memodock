using System.Windows;
using System.Windows.Input;
using MemoDock.Core.Models;
using MemoDock.Services;

namespace MemoDock;

/// <summary>应用设置窗口：配置全局快捷键与开机自启。</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action<AppSettings> _onSave;

    private ModifierKeys _capturedModifiers;
    private Key _capturedKey;
    private bool _capturing;

    public SettingsWindow(AppSettings settings, Action<AppSettings> onSave)
    {
        InitializeComponent();

        _settings = settings;
        _onSave = onSave;

        _capturedModifiers = ParseModifiers(settings.HotKeyModifiers);
        _capturedKey = ParseKey(settings.HotKeyKey);
        StartupCheck.IsChecked = settings.LaunchOnStartup;

        UpdateHotKeyDisplay();
    }

    private void HotKeyBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        BeginCapture();
    }

    private void HotKeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            CancelCapture();
            return;
        }

        // 忽略单独的修饰键按下（等待组合完成）。
        if (IsModifierKey(e.Key))
        {
            return;
        }

        var modifiers = Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows);
        if (modifiers == ModifierKeys.None)
        {
            return;
        }

        _capturedModifiers = modifiers;
        _capturedKey = e.Key;
        _capturing = false;
        UpdateHotKeyDisplay();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // 焦点在快捷键捕获框内时，Esc 应取消捕获而不是关闭窗口。
        if (e.Key == Key.Escape && !HotKeyBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_capturedKey == Key.None)
        {
            BeginCapture();
            return;
        }

        _settings.HotKeyModifiers = _capturedModifiers.ToString();
        _settings.HotKeyKey = _capturedKey.ToString();
        _settings.LaunchOnStartup = StartupCheck.IsChecked == true;

        _onSave(_settings);
        Close();
    }

    private void BeginCapture()
    {
        _capturing = true;
        HotKeyHint.Visibility = Visibility.Collapsed;
        HotKeyBox.Text = "请按下新的组合键…";
        HotKeyBox.Focus();
    }

    private void CancelCapture()
    {
        _capturing = false;
        UpdateHotKeyDisplay();
        Keyboard.ClearFocus();
    }

    private void UpdateHotKeyDisplay()
    {
        if (_capturing)
        {
            return;
        }

        HotKeyBox.Text = _capturedKey == Key.None
            ? string.Empty
            : HotKeyService.FormatCombo(_capturedModifiers, _capturedKey);
        HotKeyHint.Visibility = HotKeyBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;
    }

    private static ModifierKeys ParseModifiers(string text)
    {
        return Enum.TryParse<ModifierKeys>(text, ignoreCase: true, out var modifiers)
            ? modifiers
            : ModifierKeys.Control | ModifierKeys.Alt;
    }

    private static Key ParseKey(string text)
    {
        return Enum.TryParse<Key>(text, ignoreCase: true, out var key) ? key : Key.N;
    }
}
