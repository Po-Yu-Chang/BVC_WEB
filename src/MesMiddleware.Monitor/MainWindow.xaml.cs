using System.Windows;
using MesMiddleware.Monitor.ViewModels;
using H.NotifyIcon;

namespace MesMiddleware.Monitor;

/// <summary>
/// Main window for MES Middleware monitoring dashboard.
/// Supports system tray minimize and real-time monitoring.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _trayIcon;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        InitializeComponent();
        Loaded += MainWindow_Loaded;

        // Create system tray icon
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "MES Middleware Monitor",
            // Use default Windows system icon (Information icon)
            Icon = System.Drawing.SystemIcons.Information,
            ContextMenu = CreateTrayContextMenu()
        };
        _trayIcon.TrayMouseDoubleClick += (s, e) => ShowFromTray();

        // Show the tray icon immediately
        _trayIcon.ForceCreate();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Dispose tray icon immediately
        _trayIcon?.Dispose();

        // Fire-and-forget: Start shutdown but don't wait
        // This prevents 5-10 second delay when closing window
        _ = _viewModel.ShutdownAsync();

        // Force immediate application shutdown
        Application.Current.Shutdown();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // Minimize to system tray
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            _viewModel.HideWindowCommand.Execute(null);
        }
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _viewModel.ShowWindowCommand.Execute(null);
    }

    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var contextMenu = new System.Windows.Controls.ContextMenu();

        var showMenuItem = new System.Windows.Controls.MenuItem
        {
            Header = "顯示監控視窗"
        };
        showMenuItem.Click += (s, e) => ShowFromTray();
        contextMenu.Items.Add(showMenuItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitMenuItem = new System.Windows.Controls.MenuItem
        {
            Header = "結束程式"
        };
        exitMenuItem.Click += async (s, e) =>
        {
            _trayIcon.Dispose();
            await _viewModel.ShutdownAsync();
            Application.Current.Shutdown();
        };
        contextMenu.Items.Add(exitMenuItem);

        return contextMenu;
    }
}