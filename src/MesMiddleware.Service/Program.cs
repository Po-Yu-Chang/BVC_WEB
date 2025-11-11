using FluentValidation;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.EntityFrameworkCore;
using MesMiddleware.Service.Data;
using MesMiddleware.Service.Models;
using MesMiddleware.Service.Services.HostedServices;
using MesMiddleware.Service.Services.Queue;
using MesMiddleware.Service.Services.SharedMemory;
using MesMiddleware.Service.Services.WebApi;
using MesMiddleware.Service.Validation;
using MesMiddleware.Shared.Models;
using Serilog;
using System.Net.Http.Headers;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/middleware-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.WithProperty("Application", "MesMiddleware")
    .CreateLogger();

try
{
    Log.Information("Starting MES Middleware Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog
    builder.Services.AddSerilog();

    // Configure options from appsettings.json
    builder.Services.Configure<SharedMemoryOptions>(
        builder.Configuration.GetSection(SharedMemoryOptions.SectionName));
    builder.Services.Configure<WebApiOptions>(
        builder.Configuration.GetSection(WebApiOptions.SectionName));

    // Configure EF Core with SQLite
    var queueDbPath = builder.Configuration["Queue:DatabasePath"] ?? "Data/queue.db";
    builder.Services.AddDbContext<MiddlewareDbContext>(options =>
        options.UseSqlite($"Data Source={queueDbPath}"));

    // Configure Hangfire with SQLite
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSQLiteStorage(queueDbPath));

    builder.Services.AddHangfireServer();

    // Configure HttpClient with resilience
    var webApiBaseUrl = builder.Configuration["WebApi:BaseUrl"] ?? "http://localhost:5000";
    var webApiTimeout = builder.Configuration.GetValue<int>("WebApi:Timeout");

    builder.Services.AddHttpClient("WebApiClient", client =>
    {
        client.BaseAddress = new Uri(webApiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(webApiTimeout > 0 ? webApiTimeout : 30);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    })
    .AddStandardResilienceHandler(options =>
    {
        var retryConfig = builder.Configuration.GetSection("Resilience:Retry");
        options.Retry.MaxRetryAttempts = retryConfig.GetValue<int>("MaxRetryAttempts", 5);
        options.Retry.Delay = TimeSpan.FromSeconds(retryConfig.GetValue<int>("InitialDelay", 2));
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;

        var cbConfig = builder.Configuration.GetSection("Resilience:CircuitBreaker");
        options.CircuitBreaker.FailureRatio = cbConfig.GetValue<double>("FailureRatio", 0.5);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(cbConfig.GetValue<int>("SamplingDuration", 30));
    });

    // Register validators
    builder.Services.AddScoped<IValidator<InspectionRecord>, InspectionDataValidator>();

    // Register services
    builder.Services.AddSingleton<ISharedMemoryMonitor, SharedMemoryMonitor>();
    builder.Services.AddSingleton<ISharedMemoryWriter, SharedMemoryWriter>(); // US3: Bidirectional commands
    builder.Services.AddSingleton<ITokenService, TokenService>();
    builder.Services.AddScoped<IMesWebApiClient, MesWebApiClient>();
    builder.Services.AddScoped<IUploadQueueService, UploadQueueService>();

    // Register hosted service (main middleware orchestration)
    builder.Services.AddHostedService<MiddlewareHostedService>();

    // Configure Windows Service hosting
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "MesMiddlewareService";
    });

    var host = builder.Build();

    // Ensure database is created and migrations applied
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MiddlewareDbContext>();
        dbContext.Database.Migrate();
        Log.Information("Database migrations applied successfully");
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
