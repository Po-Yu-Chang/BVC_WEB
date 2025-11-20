using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MesMiddleware.Monitor.Data;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;
using MesMiddleware.Monitor.Services.HostedServices;
using MesMiddleware.Monitor.ViewModels;
using MesMiddleware.Shared.Models;
using Serilog;
using System.IO;
using System.Net.Http;
using System.Threading.Channels;
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
    /// Hosted services host
    /// </summary>
    private IHost? _host;

    /// <summary>
    /// Hangfire background job server
    /// </summary>
    private BackgroundJobServer? _hangfireServer;

    /// <summary>
    /// 應用程式啟動時的初始化方法
    /// </summary>
    /// <param name="e">啟動事件參數</param>
    protected override async void OnStartup(StartupEventArgs e)
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

        // 配置 Hangfire 使用自訂 JobActivator（支援依賴注入）
        GlobalConfiguration.Configuration.UseActivator(new ServiceProviderJobActivator(_serviceProvider));
        Log.Information("Hangfire JobActivator configured with DI support");

        // Start hosted services (Web API + Channel Processor)
        await StartHostedServicesAsync();

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
        // 讀取 appsettings.json 配置文件
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // 註冊 IConfiguration
        services.AddSingleton<IConfiguration>(configuration);

        // 配置日誌服務
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        // 綁定 WebApiOptions 配置
        services.Configure<WebApiOptions>(configuration.GetSection(WebApiOptions.SectionName));

        // 註冊 Entity Framework Core (SQLite)
        var connectionString = configuration.GetConnectionString("MonitorDatabase") ?? "Data Source=monitor.db";
        services.AddDbContext<MonitorDbContext>(options =>
            options.UseSqlite(connectionString));

        // 註冊 Hangfire (background job scheduler)
        // Initialize JobStorage with in-memory storage
        var storage = new Hangfire.MemoryStorage.MemoryStorage();
        JobStorage.Current = storage;

        GlobalConfiguration.Configuration
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(storage);

        // JobActivator 會在 OnStartup 中設定（需要等 ServiceProvider 建立後）

        // 註冊 BackgroundJobClient
        services.AddSingleton<IBackgroundJobClient>(provider =>
            new BackgroundJobClient(storage));

        // 註冊 BackgroundJobServer (啟動 Hangfire 工作處理器)
        services.AddSingleton(provider =>
        {
            var options = new BackgroundJobServerOptions
            {
                WorkerCount = configuration.GetValue<int>("Queue:MaxConcurrentRetries", 3)
            };
            return new BackgroundJobServer(options, storage);
        });

        // 註冊 UploadQueueService (改為 Scoped，配合 DbContext)
        services.AddScoped<IUploadQueueService, UploadQueueService>();
        services.AddScoped<RetryUploadJob>();

        // 註冊 HttpClientFactory
        services.AddHttpClient("MesCloudClient", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<WebApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.Timeout);
        });

        // 註冊 TokenService
        services.AddSingleton<ITokenService, TokenService>();

        // 註冊 Channel for inspection data (Device → Monitor queue)
        var channelOptions = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait // Wait if channel is full (backpressure)
        };
        var inspectionChannel = Channel.CreateBounded<InspectionRecord>(channelOptions);
        services.AddSingleton(inspectionChannel);

        // 註冊 MiddlewareApiClient (改為直接呼叫 MES Cloud API)
        services.AddSingleton<IMiddlewareApiClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<WebApiOptions>>().Value;
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var tokenService = sp.GetRequiredService<ITokenService>();
            var logger = sp.GetRequiredService<ILogger<MiddlewareApiClient>>();
            return new MiddlewareApiClient(
                options.BaseUrl,
                httpClientFactory.CreateClient("MesCloudClient"),
                tokenService,
                logger);
        });

        // 註冊本地化服務
        services.AddSingleton<ILocalizationService, LocalizationService>();

        // 註冊 Hosted Services (Web API + Channel Processor)
        services.AddHostedService<WebApiHostService>();
        services.AddHostedService<InspectionChannelProcessor>();

        // 註冊 ViewModels
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<TraceVerificationViewModel>();
        services.AddSingleton<MainViewModel>();

        // 註冊主視窗
        services.AddSingleton<MainWindow>();
    }

    /// <summary>
    /// 啟動後台服務 (Web API + Channel Processor)
    /// </summary>
    private async Task StartHostedServicesAsync()
    {
        if (_serviceProvider == null)
            return;

        // Ensure database is created and migrated
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MonitorDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        // Create host for background services
        var hostBuilder = new HostBuilder()
            .ConfigureServices(services =>
            {
                // Copy services from WPF DI container
                services.AddSingleton(_serviceProvider.GetRequiredService<ITokenService>());
                services.AddSingleton(_serviceProvider.GetRequiredService<IHttpClientFactory>());
                services.AddSingleton(_serviceProvider.GetRequiredService<Channel<InspectionRecord>>());
                services.AddSingleton(_serviceProvider.GetRequiredService<IOptions<WebApiOptions>>());
                services.AddSingleton(_serviceProvider.GetRequiredService<IUploadQueueService>());
                services.AddSingleton(_serviceProvider.GetRequiredService<IConfiguration>());
                services.AddSingleton(_serviceProvider);

                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddSerilog(dispose: false);
                });

                // Register hosted services
                services.AddHostedService<WebApiHostService>();
                services.AddHostedService<InspectionChannelProcessor>();
                services.AddHostedService<MesCloudConnectionMonitor>();
            });

        _host = hostBuilder.Build();

        // Start Hangfire background job server
        _hangfireServer = _serviceProvider.GetRequiredService<BackgroundJobServer>();

        // 設定週期性 Hangfire job：每 5 秒批次處理所有到期的佇列項目
        RecurringJob.AddOrUpdate<RetryUploadJob>(
            "process-pending-queue",
            job => job.ProcessPendingQueueAsync(CancellationToken.None),
            "*/5 * * * * *"); // 每 5 秒執行一次

        Log.Information("Hangfire RecurringJob configured: process-pending-queue (every 5 seconds)");

        // Start hosted services in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Hosted services crashed");
            }
        });

        Log.Information("Hosted services started (Web API on http://localhost:5100 + Channel Processor + MES Cloud Monitor + Hangfire)");
    }

    /// <summary>
    /// 應用程式結束時的清理方法
    /// </summary>
    /// <param name="e">結束事件參數</param>
    protected override async void OnExit(ExitEventArgs e)
    {
        // Stop Hangfire server
        _hangfireServer?.SendStop();
        _hangfireServer?.Dispose();

        // Stop hosted services
        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        // 釋放服務提供者資源
        _serviceProvider?.Dispose();
        // 關閉並清空日誌緩衝
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

