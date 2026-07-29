using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MemoDock.Core.Models;
using MemoDock.Core.Services;
using MemoDock.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfMessageBox = System.Windows.MessageBox;
using WpfRadioButton = System.Windows.Controls.RadioButton;

namespace MemoDock;

public partial class MainWindow : Window
{
    private readonly MemoRepository _repository;
    private readonly ForegroundAppService _foregroundApps;
    private readonly DispatcherTimer _foregroundTimer;
    private readonly HotKeyService _hotKey = new();

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
        _foregroundTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _foregroundTimer.Tick += ForegroundTimer_Tick;

        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;

        if (initialApp is not null)
        {
            SwitchToApp(initialApp);
        }
        else
        {
            SwitchToApp(new ForegroundAppSnapshot(
                new AppDescriptor("memodock.welcome", "MemoDock", string.Empty),
                null));
        }

        _foregroundTimer.Start();
    }

    public bool AllowClose { get; set; }

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
        FallbackIcon.Visibility = snapshot.Icon is null ? Visibility.Visible : Visibility.Collapsed;

        SearchBox.Clear();
        RefreshEntries();
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
            _currentNotebook.Entries.Add(new MemoEntry
            {
                Kind = _selectedKind,
                Title = editor.EntryTitle,
                Body = editor.EntryBody,
                UpdatedAt = DateTimeOffset.Now
            });
        }
        else
        {
            existing.Title = editor.EntryTitle;
            existing.Body = editor.EntryBody;
            existing.UpdatedAt = DateTimeOffset.Now;
        }

        SaveAndRefresh();
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

        _currentNotebook.Entries.Remove(entry);
        SaveAndRefresh();
    }

    private void SaveAndRefresh()
    {
        try
        {
            _repository.Save();
        }
        catch (IOException exception)
        {
            WpfMessageBox.Show(
                this,
                $"保存失败：{exception.Message}",
                "MemoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (UnauthorizedAccessException exception)
        {
            WpfMessageBox.Show(
                this,
                $"没有权限保存记录：{exception.Message}",
                "MemoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        RefreshEntries();
    }

    private void CardMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: MemoEntry entry } button)
        {
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = button
        };

        var editItem = new MenuItem { Header = "编辑" };
        editItem.Click += (_, _) => OpenEditor(entry);

        var deleteItem = new MenuItem { Header = "删除" };
        deleteItem.Click += (_, _) => DeleteEntry(entry);

        menu.Items.Add(editItem);
        menu.Items.Add(deleteItem);
        menu.IsOpen = true;
    }

    private void TodoCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfCheckBox { DataContext: MemoEntry entry })
        {
            entry.UpdatedAt = DateTimeOffset.Now;
            SaveAndRefresh();
        }
    }

    private void NewEntryButton_Click(object sender, RoutedEventArgs e)
    {
        OpenEditor();
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is WpfRadioButton { Tag: string tag })
        {
            _selectedKind = tag == "Todo" ? MemoKind.Todo : MemoKind.Note;
            if (IsLoaded)
            {
                RefreshEntries();
            }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
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
        Hide();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (AllowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void DockToRightEdge()
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width - 24);
        Height = Math.Min(Height, workArea.Height - 24);
        Left = workArea.Right - Width - 12;
        Top = workArea.Top + Math.Max(12, (workArea.Height - Height) / 2);
    }
}
