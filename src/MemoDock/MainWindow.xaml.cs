using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MemoDock.Core.Models;
using MemoDock.Core.Services;
using MemoDock.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfMessageBox = System.Windows.MessageBox;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfTextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;

namespace MemoDock;

/// <summary>MemoDock 主停靠窗口：展示当前前台软件对应的备忘录。</summary>
public partial class MainWindow : Window
{
    /// <summary>欢迎页使用的虚拟软件身份。</summary>
    private const string WelcomeAppId = "memodock.welcome";

    /// <summary>前台软件轮询间隔（兜底）：正常由 WinEvent 事件驱动，轮询仅覆盖事件漏报。</summary>
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromMilliseconds(2000);

    /// <summary>停靠到工作区右侧时的外边距。</summary>
    private const double DockEdgeMargin = 12;

    /// <summary>保存防抖间隔：连续修改合并为一次写盘。</summary>
    private static readonly TimeSpan SaveDebounceInterval = TimeSpan.FromMilliseconds(400);

    private readonly MemoRepository _repository;
    private readonly ForegroundAppService _foregroundApps;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _foregroundTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly HotKeyService _hotKey = new();
    private readonly WindowStateService _windowState = new();
    private bool _savePending;
    private bool _isSettingsOpen;
    private ModifierKeys _registeredHotKeyModifiers;
    private Key _registeredHotKeyKey;

    private AppNotebook? _currentNotebook;
    private ForegroundAppSnapshot? _currentApp;
    private MemoKind _selectedKind = MemoKind.Note;
    private bool _hasInitialPlacement;
    private bool _isRecycleBin;
    private bool _isGlobalSearch;

    public MainWindow(
        MemoRepository repository,
        ForegroundAppService foregroundApps,
        SettingsService settings,
        ForegroundAppSnapshot? initialApp)
    {
        InitializeComponent();

        _repository = repository;
        _foregroundApps = foregroundApps;
        _settings = settings;
        _hasInitialPlacement = _windowState.TryRestore(this);

        _foregroundTimer = new DispatcherTimer { Interval = ForegroundPollInterval };
        _foregroundTimer.Tick += ForegroundTimer_Tick;

        _saveTimer = new DispatcherTimer { Interval = SaveDebounceInterval };
        _saveTimer.Tick += SaveTimer_Tick;

        _foregroundApps.ForegroundChanged += ForegroundApps_ForegroundChanged;

        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;

        SwitchToApp(initialApp ?? new ForegroundAppSnapshot(
            new AppDescriptor(WelcomeAppId, "MemoDock", string.Empty),
            null));

        // 优先用 WinEvent 事件驱动；SetWinEventHook 可能因权限/兼容失败，
        // 此时轮询兜底仍保证可用。
        _foregroundApps.StartListening();
        _foregroundTimer.Start();
    }

    /// <summary>为 <c>true</c> 时允许窗口真正关闭（托盘退出时设置）。</summary>
    public bool AllowClose { get; set; }

    /// <summary>显示并激活窗口；首次显示时停靠到工作区右侧。</summary>
    public void ShowDock()
    {
        if (!_hasInitialPlacement)
        {
            DockToRightEdge();
            _hasInitialPlacement = true;
        }

        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        Activate();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        WindowEffects.Apply(this);
        RegisterHotKey();
    }

    /// <summary>用当前设置注册全局快捷键，并更新提示文本。</summary>
    private void RegisterHotKey()
    {
        var modifiers = ParseModifiers(_settings.Current.HotKeyModifiers);
        var key = ParseKey(_settings.Current.HotKeyKey);
        _registeredHotKeyModifiers = modifiers;
        _registeredHotKeyKey = key;
        _hotKey.Register(this, modifiers, key, HandleGlobalHotKey);

        var comboText = HotKeyService.FormatCombo(modifiers, key);
        HotKeyHint.Text = _hotKey.IsRegistered
            ? comboText
            : $"{comboText} 已被占用";
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    /// <summary>打开设置窗口；保存前验证新快捷键可用，失败则不写入设置，保持旧热键有效。</summary>
    public void OpenSettings()
    {
        if (_isSettingsOpen)
        {
            return;
        }

        _isSettingsOpen = true;
        try
        {
            var window = new SettingsWindow(_settings.Current, onSave: settings =>
            {
                if (!TryValidateAndApplyHotKey(settings))
                {
                    return;
                }

                _settings.Save(settings);
                RegisterHotKey();
                ApplyStartupSetting();
            })
            {
                Owner = this
            };
            window.ShowDialog();
        }
        finally
        {
            _isSettingsOpen = false;
        }
    }

    /// <summary>校验新快捷键：被占用或属常见系统组合时拦截；通过才返回 true。</summary>
    private bool TryValidateAndApplyHotKey(AppSettings settings)
    {
        var modifiers = ParseModifiers(settings.HotKeyModifiers);
        var key = ParseKey(settings.HotKeyKey);
        var comboText = HotKeyService.FormatCombo(modifiers, key);

        // 与当前已注册组合相同时无需重注册，直接接受。
        var isSameAsCurrent =
            _hotKey.IsRegistered &&
            modifiers == _registeredHotKeyModifiers &&
            key == _registeredHotKeyKey;

        if (!isSameAsCurrent && !_hotKey.IsCombinationAvailable(modifiers, key))
        {
            WpfMessageBox.Show(
                this,
                $"快捷键 {comboText} 已被其他程序占用，未保存。",
                "MemoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        if (IsCommonSystemCombination(modifiers, key))
        {
            var result = WpfMessageBox.Show(
                this,
                $"{comboText} 是常用的系统快捷键（如 Ctrl+C 复制），"
                + "注册为全局快捷键后可能影响其他应用。仍要继续吗？",
                "MemoDock",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>判断组合是否可能劫持常用系统/剪贴板快捷键。</summary>
    private static bool IsCommonSystemCombination(ModifierKeys modifiers, Key key)
    {
        if (modifiers == ModifierKeys.Control && key is
            Key.C or Key.V or Key.X or Key.Z or Key.Y or Key.A or Key.S or Key.O or Key.P)
        {
            return true;
        }

        if (modifiers == ModifierKeys.Windows)
        {
            return true;
        }

        return false;
    }

    /// <summary>把设置中的开机自启选项同步到注册表；仅在需要变更时写入。</summary>
    private void ApplyStartupSetting()
    {
        var desired = _settings.Current.LaunchOnStartup;
        if (StartupService.IsEnabled() == desired)
        {
            return;
        }

        StartupService.SetEnabled(desired);
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

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _foregroundTimer.Stop();
        _saveTimer.Stop();
        _foregroundApps.ForegroundChanged -= ForegroundApps_ForegroundChanged;
        _foregroundApps.Dispose();
        _hotKey.Dispose();
    }

    /// <summary>WinEvent 事件驱动的前台切换通知。</summary>
    private void ForegroundApps_ForegroundChanged(object? sender, EventArgs e)
    {
        HandleForegroundPoll();
    }

    /// <summary>轮询兜底（事件漏报时保证可用）。</summary>
    private void ForegroundTimer_Tick(object? sender, EventArgs e)
    {
        HandleForegroundPoll();
    }

    /// <summary>检查前台软件是否变化，变化时切换；未开启自动跟随或识别失败时忽略。</summary>
    private void HandleForegroundPoll()
    {
        if (AutoFollowToggle.IsChecked != true)
        {
            return;
        }

        var snapshot = _foregroundApps.TryGetForegroundApp();
        if (snapshot is null)
        {
            return;
        }

        if (string.Equals(
                snapshot.Descriptor.AppId,
                _currentApp?.Descriptor.AppId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SwitchToApp(snapshot);
    }

    private void SwitchToApp(ForegroundAppSnapshot snapshot)
    {
        _currentApp = snapshot;
        _currentNotebook = _repository.GetOrCreateNotebook(snapshot.Descriptor);

        CurrentAppName.Text = snapshot.Descriptor.DisplayName;
        CurrentAppIcon.Source = snapshot.Icon;
        CurrentAppIcon.Visibility = snapshot.Icon is null ? Visibility.Collapsed : Visibility.Visible;
        FallbackIcon.Text = GetFallbackLetter(snapshot.Descriptor.DisplayName);
        FallbackIcon.Visibility = snapshot.Icon is null ? Visibility.Visible : Visibility.Collapsed;

        GlobalSearchToggle.IsChecked = false;
        _isGlobalSearch = false;
        SearchBox.Clear();
        RefreshEntries();
    }

    /// <summary>取软件名的首字母作为占位符；无法确定时回退为问号。</summary>
    private static string GetFallbackLetter(string displayName)
    {
        foreach (var character in displayName.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                return character.ToString().ToUpperInvariant();
            }
        }

        return "?";
    }

    private void AppSwitchButton_Click(object sender, RoutedEventArgs e)
    {
        var notebooks = _repository.Database.Apps
            .Where(notebook => !string.Equals(
                notebook.AppId,
                WelcomeAppId,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(notebook => notebook.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var menu = ContextMenuBuilder.CreateAppSwitchMenu(
            AppSwitchButton,
            notebooks,
            _currentApp?.Descriptor.AppId,
            notebook =>
            {
                AutoFollowToggle.IsChecked = false;
                UpdateFollowModeHint();
                SwitchToApp(_foregroundApps.CreateSnapshot(new AppDescriptor(
                    notebook.AppId,
                    notebook.DisplayName,
                    notebook.ExecutablePath)));
            });

        menu.IsOpen = true;
    }

    private void AutoFollowToggle_Click(object sender, RoutedEventArgs e)
    {
        UpdateFollowModeHint();
    }

    private void UpdateFollowModeHint()
    {
        FollowModeHint.Text = AutoFollowToggle.IsChecked == true
            ? "自动跟随前台软件"
            : "已锁定 · 点击软件名切换";
    }

    private void RefreshEntries()
    {
        if (_currentNotebook is null)
        {
            EntryList.ItemsSource = null;
            ShowEmptyState(true, "还没有记录", "点击下方按钮添加第一条");
            return;
        }

        if (_isRecycleBin)
        {
            NewEntryButton.Visibility = Visibility.Collapsed;
            var deleted = MemoQuery
                .FilterDeleted(_currentNotebook.Entries, SearchBox.Text)
                .ToList();
            EntryList.ItemsSource = deleted.Select(entry => new EntryView(entry)).ToList();
            ShowEmptyState(deleted.Count == 0, "回收站是空的", "删除的记录会保留在这里，可恢复或彻底删除");
            return;
        }

        NewEntryButton.Visibility = Visibility.Visible;

        if (_isGlobalSearch)
        {
            var results = MemoQuery
                .SearchAll(
                    _repository.Database.Apps.Where(notebook =>
                        !string.Equals(notebook.AppId, WelcomeAppId, StringComparison.OrdinalIgnoreCase)),
                    SearchBox.Text)
                .ToList();
            EntryList.ItemsSource = results
                .Select(result => new EntryView(result.Entry, result.Notebook.DisplayName))
                .ToList();
            ShowEmptyState(results.Count == 0, "没有找到匹配的记录", "试试其他关键词，或关闭全局搜索");
            return;
        }

        var entries = MemoQuery
            .Filter(_currentNotebook.Entries, _selectedKind, SearchBox.Text)
            .ToList();

        EntryList.ItemsSource = entries.Select(entry => new EntryView(entry)).ToList();
        ShowEmptyState(entries.Count == 0, "还没有记录", "点击下方按钮添加第一条");
    }

    private void ShowEmptyState(bool isEmpty, string title, string hint)
    {
        EmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateTitle.Text = title;
        EmptyStateHint.Text = hint;
    }

    private void HandleGlobalHotKey()
    {
        ShowDock();
    }

    private void OpenEditor(MemoEntry? existing = null)
    {
        if (_currentNotebook is null)
        {
            return;
        }

        var editor = new EditorWindow(existing?.Kind ?? _selectedKind, existing)
        {
            Owner = this
        };

        if (editor.ShowDialog() != true)
        {
            return;
        }

        if (existing is null)
        {
            var entry = new MemoEntry
            {
                Kind = _selectedKind,
                Title = editor.EntryTitle,
                Body = editor.EntryBody,
                CreatedAt = DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            };

            CommitMutation(() => _currentNotebook.Entries.Add(entry));
        }
        else
        {
            CommitMutation(() =>
            {
                existing.Title = editor.EntryTitle;
                existing.Body = editor.EntryBody;
                existing.UpdatedAt = DateTimeOffset.Now;
            });
        }
    }

    private void DeleteEntry(MemoEntry entry)
    {
        var result = WpfMessageBox.Show(
            this,
            $"确定删除“{entry.Title}”吗？",
            "删除记录",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // 软删除：记录仍保留在数据中，可从回收站恢复。
        CommitMutation(() => entry.IsDeleted = true);
    }

    private void RestoreEntry(MemoEntry entry)
    {
        CommitMutation(() => entry.IsDeleted = false);
    }

    private void DeleteForever(MemoEntry entry)
    {
        if (_currentNotebook is null)
        {
            return;
        }

        var result = WpfMessageBox.Show(
            this,
            $"彻底删除“{entry.Title}”吗？此操作无法撤销。",
            "彻底删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var index = _currentNotebook.Entries.IndexOf(entry);
        CommitMutation(() => _currentNotebook.Entries.Remove(entry));
    }

    private void TodoCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfCheckBox { DataContext: EntryView { Entry: MemoEntry entry } })
        {
            return;
        }

        // 双向绑定已在 Click 前把 IsCompleted 更新为勾选后的新值。
        CommitMutation(() => entry.UpdatedAt = DateTimeOffset.Now);
    }

    /// <summary>执行内存修改并立即刷新列表，落盘通过防抖定时器合并。</summary>
    private void CommitMutation(Action mutate)
    {
        mutate();
        RefreshEntries();
        ScheduleSave();
    }

    /// <summary>启动防抖保存；已有未落盘修改时保持等待，合并为一次写盘。</summary>
    private void ScheduleSave()
    {
        _savePending = true;
        _saveTimer.Start();
    }

    private void SaveTimer_Tick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        if (!_savePending)
        {
            return;
        }

        _savePending = false;
        if (!TrySave())
        {
            // 保存失败：放弃未落盘的改动，从磁盘恢复到最近一次成功状态。
            ReloadCurrentNotebook();
        }
    }

    /// <summary>隐藏窗口或退出前强制写盘未保存的修改。</summary>
    private void FlushPendingSave()
    {
        if (!_savePending)
        {
            return;
        }

        _saveTimer.Stop();
        _savePending = false;
        TrySave();
    }

    /// <summary>从磁盘重新加载数据库并重新绑定当前软件，保留搜索与页签状态。</summary>
    private void ReloadCurrentNotebook()
    {
        try
        {
            _repository.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        if (_currentApp is not null)
        {
            _currentNotebook = _repository.GetOrCreateNotebook(_currentApp.Descriptor);
        }

        RefreshEntries();
    }

    private bool TrySave()
    {
        try
        {
            _repository.Save();
            return true;
        }
        catch (IOException exception)
        {
            WpfMessageBox.Show(
                this,
                $"保存失败：{exception.Message}",
                "MemoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            WpfMessageBox.Show(
                this,
                $"没有权限保存记录：{exception.Message}",
                "MemoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private void CardMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: EntryView view } button)
        {
            return;
        }

        var menu = _isRecycleBin
            ? ContextMenuBuilder.CreateRecycleMenu(
                button,
                view.Entry,
                onRestore: RestoreEntry,
                onDeleteForever: DeleteForever)
            : ContextMenuBuilder.CreateCardMenu(
                button,
                view.Entry,
                onEdit: OpenEditor,
                onDelete: DeleteEntry);

        menu.IsOpen = true;
    }

    private void NewEntryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenEditor();
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfRadioButton { Tag: string tag })
        {
            return;
        }

        _isRecycleBin = tag == "Recycle";
        if (_isRecycleBin)
        {
            GlobalSearchToggle.IsChecked = false;
            _isGlobalSearch = false;
        }
        else
        {
            _selectedKind = tag == "Todo" ? MemoKind.Todo : MemoKind.Note;
        }

        if (IsLoaded)
        {
            RefreshEntries();
        }
    }

    private void GlobalSearchToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle)
        {
            return;
        }

        if (_isRecycleBin)
        {
            // 回收站模式下全局搜索不生效，保持开关关闭。
            toggle.IsChecked = false;
            return;
        }

        _isGlobalSearch = toggle.IsChecked == true;
        RefreshEntries();
    }

    private void SearchBox_TextChanged(object sender, WpfTextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        if (IsLoaded)
        {
            RefreshEntries();
        }
    }

    private void SearchBox_FocusChanged(object sender, RoutedEventArgs e)
    {
        UpdateSearchPlaceholder();
    }

    private void UpdateSearchPlaceholder()
    {
        if (SearchPlaceholder is null || SearchBox is null)
        {
            return;
        }

        SearchPlaceholder.Visibility =
            string.IsNullOrEmpty(SearchBox.Text) && !SearchBox.IsKeyboardFocused
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideDock();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideDock();
            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        if (e.Key == Key.N && !_isRecycleBin)
        {
            OpenEditor();
            e.Handled = true;
        }
        else if (e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            FlushPendingSave();
            _windowState.Save(this);
            return;
        }

        e.Cancel = true;
        HideDock();
    }

    private void HideDock()
    {
        FlushPendingSave();
        _windowState.Save(this);
        Hide();
    }

    /// <summary>
    /// 停靠到前台软件所在显示器右侧；无法确定前台显示器时回退到主工作区。
    /// </summary>
    private void DockToRightEdge()
    {
        var workArea = TryGetForegroundWorkArea() ?? PrimaryWorkArea;
        Width = Math.Min(Width, workArea.Width - DockEdgeMargin * 2);
        Height = Math.Min(Height, workArea.Height - DockEdgeMargin * 2);
        Left = workArea.Right - Width - DockEdgeMargin;
        Top = workArea.Top + Math.Max(DockEdgeMargin, (workArea.Height - Height) / 2);
    }

    private static Rect PrimaryWorkArea => SystemParameters.WorkArea;

    /// <summary>取前台窗口所在显示器的工作区（换算为设备无关单位）；失败时返回 <c>null</c>。</summary>
    private Rect? TryGetForegroundWorkArea()
    {
        var windowHandle = _foregroundApps.TryGetForegroundWindowHandle();
        if (windowHandle == IntPtr.Zero)
        {
            return null;
        }

        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return null;
        }

        return MonitorWorkAreaToDips(info);
    }

    private Rect MonitorWorkAreaToDips(MonitorInfo info)
    {
        // 显示器工作区是物理像素。进程默认 SystemAware 时 WPF 的 DIP 坐标系
        // 按系统 DPI 解释（而非目标显示器 DPI），因此换算必须用系统 DPI；
        // 若用目标屏 DPI（GetDpiForMonitor），混 DPI 环境下会坐标错位。
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var left = info.Work.Left * scale;
        var top = info.Work.Top * scale;
        return new Rect(
            left,
            top,
            (info.Work.Right - info.Work.Left) * scale,
            (info.Work.Bottom - info.Work.Top) * scale);
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    /// <summary>列表卡片的视图包装：记录本身及其所属软件名（全局搜索时显示）。</summary>
    private sealed class EntryView(MemoEntry entry, string appName = "")
    {
        public MemoEntry Entry { get; } = entry;

        public string AppName { get; } = appName;

        public bool HasAppName => AppName.Length > 0;
    }
}
