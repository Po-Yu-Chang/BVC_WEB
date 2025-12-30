using System.Windows;
using MesMiddleware.Monitor.ViewModels;
using H.NotifyIcon;

namespace MesMiddleware.Monitor;

/// <summary>
/// MES 中介軟體監控儀表板主視窗
/// 支援系統匣最小化與即時監控功能
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _trayIcon;

    /// <summary>
    /// 建構函式，初始化主視窗與系統匣圖示
    /// </summary>
    /// <param name="viewModel">主視圖模型</param>
    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        InitializeComponent();
        Loaded += MainWindow_Loaded;

        // 建立系統匣圖示
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "MES Middleware Monitor",
            // 使用 Windows 預設的資訊圖示
            Icon = System.Drawing.SystemIcons.Information,
            ContextMenu = CreateTrayContextMenu()
        };
        // 雙擊系統匣圖示時顯示視窗
        _trayIcon.TrayMouseDoubleClick += (s, e) => ShowFromTray();

        // 立即顯示系統匣圖示
        _trayIcon.ForceCreate();
    }

    /// <summary>
    /// 視窗載入完成時的處理方法
    /// </summary>
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    /// <summary>
    /// 視窗關閉時的處理方法
    /// </summary>
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 釋放系統匣圖示
        try { _trayIcon?.Dispose(); } catch { }

        // 停止 ViewModel 的定期刷新
        try { _viewModel.StopAllRefresh(); } catch { }

        // 強制終止進程
        Environment.Exit(0);
    }

    /// <summary>
    /// 視窗狀態改變時的處理方法
    /// </summary>
    private void Window_StateChanged(object? sender, EventArgs e)
    {
        // 最小化到系統匣
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            _viewModel.HideWindowCommand.Execute(null);
        }
    }

    /// <summary>
    /// 從系統匣還原視窗
    /// </summary>
    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _viewModel.ShowWindowCommand.Execute(null);
    }

    /// <summary>
    /// 建立系統匣右鍵選單
    /// </summary>
    /// <returns>系統匣選單物件</returns>
    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var contextMenu = new System.Windows.Controls.ContextMenu();

        // 顯示視窗選單項目
        var showMenuItem = new System.Windows.Controls.MenuItem();
        showMenuItem.SetBinding(System.Windows.Controls.MenuItem.HeaderProperty,
            new System.Windows.Data.Binding("LocalizationService.TrayMenuShow") { Source = _viewModel });
        showMenuItem.Click += (s, e) => ShowFromTray();
        contextMenu.Items.Add(showMenuItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        // 結束程式選單項目
        var exitMenuItem = new System.Windows.Controls.MenuItem();
        exitMenuItem.SetBinding(System.Windows.Controls.MenuItem.HeaderProperty,
            new System.Windows.Data.Binding("LocalizationService.TrayMenuExit") { Source = _viewModel });
        exitMenuItem.Click += (s, e) =>
        {
            try { _trayIcon?.Dispose(); } catch { }
            try { _viewModel.StopAllRefresh(); } catch { }
            Environment.Exit(0);
        };
        contextMenu.Items.Add(exitMenuItem);

        return contextMenu;
    }
}