using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
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

    /// <summary>前台软件轮询间隔。</summary>
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromMilliseconds(800);

    /// <summary>停靠到工作区右侧时的外边距。</summary>
    private const double DockEdgeMargin = 12;

    private readonly MemoRepository _repository;
    private readonly ForegroundAppService _foregroundApps;
    private readonly DispatcherTimer _foregroundTimer;
    private readonly HotKeyService _hotKey = new();
    private readonly WindowStateService _windowState = new();

    private AppNotebook? _currentNotebook;
    private ForegroundAppSnapshot? _currentApp;
    private MemoKind _selectedKind = MemoKind.Note;
    private bool _hasInitialPlacement;

    public MainWindow(
        MemoRepository repository,
        ForegroundAppService foregroundApps,
        ForegroundAppSnapshot? initialApp)
    {
        InitializeComponent();

        _repository = repository;
        _foregroundApps = foregroundApps;
        _hasInitialPlacement = _windowState.TryRestore(this);

        _foregroundTimer = new DispatcherTimer { Interval = ForegroundPollInterval };
        _foregroundTimer.Tick += ForegroundTimer_Tick;

        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;

        SwitchToApp(initialApp ?? new ForegroundAppSnapshot(
            new AppDescriptor(WelcomeAppId, "MemoDock", string.Empty),
            null));

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
        _hotKey.Register(this, HandleGlobalHotKey);
        if (!_hotKey.IsRegistered)
        {
            HotKeyHint.Text = "Ctrl + Alt + N 已被占用";
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _foregroundTimer.Stop();
        _hotKey.Dispose();
    }

    private void ForegroundTimer_Tick(object? sender, EventArgs e)
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
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        var entries = MemoQuery
            .Filter(_currentNotebook.Entries, _selectedKind, SearchBox.Text)
            .ToList();

        EntryList.ItemsSource = entries;
        EmptyState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

        var editor = new EditorWindow(_selectedKind, existing)
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
                UpdatedAt = DateTimeOffset.Now
            };

            MutateWithRollback(
                mutate: () => _currentNotebook.Entries.Add(entry),
                rollback: () => _currentNotebook.Entries.Remove(entry));
        }
        else
        {
            var previousTitle = existing.Title;
            var previousBody = existing.Body;
            var previousUpdatedAt = existing.UpdatedAt;

            MutateWithRollback(
                mutate: () =>
                {
                    existing.Title = editor.EntryTitle;
                    existing.Body = editor.EntryBody;
                    existing.UpdatedAt = DateTimeOffset.Now;
                },
                rollback: () =>
                {
                    existing.Title = previousTitle;
                    existing.Body = previousBody;
                    existing.UpdatedAt = previousUpdatedAt;
                });
        }
    }

    private void DeleteEntry(MemoEntry entry)
    {
        if (_currentNotebook is null)
        {
            return;
        }

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

        var index = _currentNotebook.Entries.IndexOf(entry);
        MutateWithRollback(
            mutate: () => _currentNotebook.Entries.Remove(entry),
            rollback: () => _currentNotebook.Entries.Insert(index, entry));
    }

    private void TodoCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfCheckBox { DataContext: MemoEntry entry })
        {
            return;
        }

        // 双向绑定已在 Click 前把 IsCompleted 更新为勾选后的新值，
        // 因此取反得到勾选前的旧值，用于保存失败时回滚。
        var previousCompleted = !entry.IsCompleted;
        var previousUpdatedAt = entry.UpdatedAt;

        MutateWithRollback(
            mutate: () => entry.UpdatedAt = DateTimeOffset.Now,
            rollback: () =>
            {
                entry.IsCompleted = previousCompleted;
                entry.UpdatedAt = previousUpdatedAt;
            });
    }

    /// <summary>先执行修改，保存失败时回滚并刷新列表。</summary>
    private void MutateWithRollback(Action mutate, Action rollback)
    {
        mutate();
        if (!TrySaveAndRefresh())
        {
            rollback();
            RefreshEntries();
        }
    }

    private bool TrySaveAndRefresh()
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
        finally
        {
            RefreshEntries();
        }
    }

    private void CardMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: MemoEntry entry } button)
        {
            return;
        }

        var menu = ContextMenuBuilder.CreateCardMenu(
            button,
            entry,
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

        _selectedKind = tag == "Todo" ? MemoKind.Todo : MemoKind.Note;
        if (IsLoaded)
        {
            RefreshEntries();
        }
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
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            _windowState.Save(this);
            return;
        }

        e.Cancel = true;
        HideDock();
    }

    private void HideDock()
    {
        _windowState.Save(this);
        Hide();
    }

    private void DockToRightEdge()
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - DockEdgeMargin * 2);
        Height = Math.Min(Height, workArea.Height - DockEdgeMargin * 2);
        Left = workArea.Right - Width - DockEdgeMargin;
        Top = workArea.Top + Math.Max(DockEdgeMargin, (workArea.Height - Height) / 2);
    }
}
