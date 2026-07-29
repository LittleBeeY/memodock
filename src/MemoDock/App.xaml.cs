using System.Drawing;
using System.Windows;
using MemoDock.Core.Services;
using MemoDock.Services;

namespace MemoDock;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var repository = new MemoRepository();
        repository.Load();

        var foregroundApps = new ForegroundAppService();
        var initialApp = foregroundApps.TryGetForegroundApp();

        _mainWindow = new MainWindow(repository, foregroundApps, initialApp);
        MainWindow = _mainWindow;

        CreateTrayIcon();
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
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示 MemoDock", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "MemoDock",
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
    }
}
