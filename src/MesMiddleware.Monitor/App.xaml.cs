using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Services;
using MesMiddleware.Monitor.ViewModels;
using Serilog;
using System.IO;
using System.Windows;

namespace MesMiddleware.Monitor;

/// <summary>
/// WPF Application with dependency injection and logging.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/monitor-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        // Configure dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        // Configuration (database path from appsettings or default)
        var databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "queue.db");

        // Services
        services.AddSingleton<IMiddlewareApiClient>(sp =>
            new MiddlewareApiClient(
                databasePath,
                sp.GetRequiredService<ILogger<MiddlewareApiClient>>()));

        // ViewModels
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<QueueViewModel>();
        services.AddSingleton<MainViewModel>();

        // Main Window
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

