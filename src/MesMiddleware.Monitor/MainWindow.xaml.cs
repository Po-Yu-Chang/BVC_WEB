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
            ToolTipText = "MES Middleware Monitor"
        };
        _trayIcon.TrayMouseDoubleClick += (s, e) => ShowFromTray();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _trayIcon.Dispose();
        await _viewModel.ShutdownAsync();
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
}