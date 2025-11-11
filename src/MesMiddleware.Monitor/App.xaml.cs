using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Services;
using MesMiddleware.Monitor.ViewModels;
using Serilog;
using System.IO;
using System.Windows;

namespace MesMiddleware.Monitor;

/// <summary>
/// WPF 應用程式主類別，整合依賴注入和日誌記錄功能
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 服務提供者，用於管理依賴注入容器
    /// </summary>
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// 應用程式啟動時的初始化方法
    /// </summary>
    /// <param name="e">啟動事件參數</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 配置 Serilog 日誌系統
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console() // 輸出到控制台
            .WriteTo.File("logs/monitor-.log", // 輸出到檔案
                rollingInterval: RollingInterval.Day, // 每天建立新檔案
                retainedFileCountLimit: 7) // 保留 7 天的日誌
            .CreateLogger();

        // 配置依賴注入容器
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 顯示主視窗
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// 配置依賴注入服務
    /// </summary>
    /// <param name="services">服務集合</param>
    private void ConfigureServices(IServiceCollection services)
    {
        // 配置日誌服務
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        // 配置資料庫路徑（從 appsettings 讀取或使用預設值）
        var databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "queue.db");

        // 註冊服務
        services.AddSingleton<IMiddlewareApiClient>(sp =>
            new MiddlewareApiClient(
                databasePath,
                sp.GetRequiredService<ILogger<MiddlewareApiClient>>()));

        // 註冊本地化服務
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // 註冊 ViewModels
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<MainViewModel>();

        // 註冊主視窗
        services.AddSingleton<MainWindow>();
    }

    /// <summary>
    /// 應用程式結束時的清理方法
    /// </summary>
    /// <param name="e">結束事件參數</param>
    protected override void OnExit(ExitEventArgs e)
    {
        // 釋放服務提供者資源
        _serviceProvider?.Dispose();
        // 關閉並清空日誌緩衝
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

