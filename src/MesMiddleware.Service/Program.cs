using FluentValidation;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.EntityFrameworkCore;
using MesMiddleware.Service.Data;
using MesMiddleware.Service.Models;
using MesMiddleware.Service.Services.HostedServices;
using MesMiddleware.Service.Services.Queue;
using MesMiddleware.Service.Services.WebApi;
using MesMiddleware.Service.Validation;
using MesMiddleware.Shared.Models;
using Serilog;
using System.Net.Http.Headers;
using System.Threading.Channels;

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
    Log.Information("Starting MES Middleware Service with Web API host");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Services.AddSerilog();

    // Add ASP.NET Core services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Configure options from appsettings.json
    builder.Services.Configure<WebApiOptions>(
        builder.Configuration.GetSection(WebApiOptions.SectionName));

    // Configure in-memory channel for inspection data (replaces shared memory)
    var channelOptions = new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.Wait // Wait if channel is full (backpressure)
    };
    var inspectionChannel = Channel.CreateBounded<InspectionRecord>(channelOptions);
    builder.Services.AddSingleton(inspectionChannel);

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

    // Register services (removed SharedMemoryMonitor/Writer)
    builder.Services.AddSingleton<ITokenService, TokenService>();
    builder.Services.AddScoped<IMesWebApiClient, MesWebApiClient>();
    builder.Services.AddScoped<IUploadQueueService, UploadQueueService>();

    // Register hosted service (replaces MiddlewareHostedService)
    builder.Services.AddHostedService<InspectionChannelProcessor>();

    // Configure Windows Service hosting
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "MesMiddlewareService";
    });

    var app = builder.Build();

    // Configure HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthorization();
    app.MapControllers();

    // Map Hangfire dashboard
    app.MapHangfireDashboard();

    // Ensure database is created and migrations applied
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MiddlewareDbContext>();
        dbContext.Database.Migrate();
        Log.Information("Database migrations applied successfully");
    }

    Log.Information("Web API listening on configured URLs (check appsettings.json)");
    await app.RunAsync();
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

// Make Program class public for integration tests (WebApplicationFactory)
public partial class Program { }
