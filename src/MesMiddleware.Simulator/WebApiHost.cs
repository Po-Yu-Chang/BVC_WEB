using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MesMiddleware.Simulator.Data;
using MesMiddleware.Simulator.Services;
using MesMiddleware.Simulator.Middleware;
using Serilog;

namespace MesMiddleware.Simulator;

/// <summary>
/// Web API Host - 在 WPF 應用程式中啟動 ASP.NET Core Web API
/// </summary>
public class WebApiHost
{
    private WebApplication? _app;
    private Task? _runTask;

    public bool IsRunning => _app != null && _runTask != null && !_runTask.IsCompleted;

    /// <summary>
    /// 啟動 Web API 服務器
    /// </summary>
    public async Task StartAsync(string url = "http://localhost:5200")
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Web API 已經在運行中");
        }

        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/simulator-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var builder = WebApplication.CreateBuilder();

        // 配置監聽 URL
        builder.WebHost.UseUrls(url);

        // 配置日誌
        builder.Host.UseSerilog();

        // 配置服務
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new()
            {
                Title = "MES Cloud Simulator API",
                Version = "v1",
                Description = "易美科 MES 系統模擬器 - 用於測試 MES Middleware Service"
            });
        });

        // 配置 SQLite 數據庫
        builder.Services.AddDbContext<SimulatorDbContext>(options =>
            options.UseSqlite("Data Source=simulator.db"));

        // 配置服務
        builder.Services.AddScoped<TokenService>();
        builder.Services.AddSingleton<SimulationConfigService>();

        // 配置 CORS (開發環境)
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        _app = builder.Build();

        // 初始化數據庫
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimulatorDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        // 配置中間件管道
        if (_app.Environment.IsDevelopment() || true)
        {
            _app.UseSwagger();
            _app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MES Simulator API v1");
                c.RoutePrefix = "swagger";
            });
        }

        _app.UseCors();

        // 請求日誌中間件 (記錄所有通訊資料)
        _app.UseMiddleware<RequestLoggingMiddleware>();

        _app.MapControllers();

        // 啟動服務器 (非阻塞)
        _runTask = _app.RunAsync();

        Log.Information("MES Cloud Simulator 已啟動: {Url}", url);
        await Task.Delay(500); // 等待服務器完全啟動
    }

    /// <summary>
    /// 停止 Web API 服務器
    /// </summary>
    public async Task StopAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            _app = null;
            _runTask = null;
            Log.Information("MES Cloud Simulator 已停止");
        }
    }

    /// <summary>
    /// 獲取模擬配置服務 (用於 UI 修改設定)
    /// </summary>
    public SimulationConfigService? GetConfigService()
    {
        return _app?.Services.GetService<SimulationConfigService>();
    }

    /// <summary>
    /// 獲取數據庫上下文 (用於 UI 查詢數據)
    /// </summary>
    public IServiceScope CreateScope()
    {
        if (_app == null)
            throw new InvalidOperationException("Web API 未運行");

        return _app.Services.CreateScope();
    }
}
