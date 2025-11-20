using System.Windows;
using MesMiddleware.Simulator.ViewModels;

namespace MesMiddleware.Simulator;

/// <summary>
/// MES Cloud Simulator 應用程式
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 創建依賴注入容器
        var webApiHost = new WebApiHost();
        var mainViewModel = new MainViewModel(webApiHost);

        // 創建並顯示主視窗
        var mainWindow = new MainWindow(mainViewModel);
        mainWindow.Show();
    }
}
