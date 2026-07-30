using System.Drawing;
using System.IO;
using System.Windows;
using MemoDock.Core.Services;
using MemoDock.Services;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MemoDock;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private MemoRepository? _repository;
    private SingleInstanceService? _singleInstance;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private Icon? _applicationIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.SignalPrimary();
            Shutdown();
            return;
        }

        _repository = new MemoRepository();
        try
        {
            _repository.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(
                $"无法读取本地记录：{exception.Message}",
                "MemoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var foregroundApps = new ForegroundAppService();
        var initialApp = foregroundApps.TryGetForegroundApp();

        _mainWindow = new MainWindow(_repository, foregroundApps, initialApp);
        MainWindow = _mainWindow;

        CreateTrayIcon();
        _singleInstance.Listen(() => Dispatcher.Invoke(ShowMainWindow));
        _mainWindow.ShowDock();
    }

    internal void ShowMainWindow()
    {
        _mainWindow?.ShowDock();
    }

    internal void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
            _mainWindow.Close();
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        _applicationIcon?.Dispose();
        _applicationIcon = null;
        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示 MemoDock", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        menu.Items.Add("导出数据备份…", null, (_, _) => Dispatcher.Invoke(ExportBackup));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _applicationIcon = TryLoadApplicationIcon();
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _applicationIcon ?? SystemIcons.Application,
            Text = "MemoDock",
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
    }

    private static Icon? TryLoadApplicationIcon()
    {
        var executablePath = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(executablePath)
            ? null
            : Icon.ExtractAssociatedIcon(executablePath);
    }

    private void ExportBackup()
    {
        if (_repository is null)
        {
            return;
        }

        var dialog = new WpfSaveFileDialog
        {
            Title = "导出 MemoDock 数据备份",
            FileName = $"MemoDock-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            DefaultExt = ".json",
            Filter = "JSON 文件 (*.json)|*.json"
        };

        if (dialog.ShowDialog(_mainWindow) != true)
        {
            return;
        }

        try
        {
            _repository.ExportTo(dialog.FileName);
            System.Windows.MessageBox.Show(
                _mainWindow,
                "数据备份已导出。",
                "MemoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(
                _mainWindow,
                $"导出失败：{exception.Message}",
                "MemoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
