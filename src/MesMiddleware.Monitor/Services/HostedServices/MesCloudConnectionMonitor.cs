using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace MesMiddleware.Monitor.Services.HostedServices;

/// <summary>
/// 監控 MES Cloud 連線狀態的背景服務
/// 如果斷線，每 5 秒自動嘗試重新連線
/// </summary>
public class MesCloudConnectionMonitor : BackgroundService
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<MesCloudConnectionMonitor> _logger;
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromSeconds(5);

    public MesCloudConnectionMonitor(
        ITokenService tokenService,
        ILogger<MesCloudConnectionMonitor> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MES Cloud connection monitor starting...");

        // 啟動時先嘗試連線一次
        await TryConnectToMesCloudAsync(stoppingToken);

        // 定期檢查連線狀態
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_reconnectInterval, stoppingToken);

                // 檢查 Token 是否有效
                if (!_tokenService.IsTokenValid())
                {
                    _logger.LogWarning("MES Cloud token is invalid or expired, attempting to reconnect...");
                    await TryConnectToMesCloudAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("MES Cloud connection monitor is stopping (cancellation requested)");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MES Cloud connection monitor loop");
            }
        }

        _logger.LogInformation("MES Cloud connection monitor stopped");
    }

    /// <summary>
    /// 嘗試連線到 MES Cloud
    /// </summary>
    private async Task TryConnectToMesCloudAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting to connect to MES Cloud...");
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("Successfully connected to MES Cloud");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to MES Cloud - HTTP request failed (will retry in {Interval})", _reconnectInterval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MES Cloud (will retry in {Interval})", _reconnectInterval);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MES Cloud connection monitor shutdown initiated");

        try
        {
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("MES Cloud connection monitor shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception during MES Cloud connection monitor shutdown");
        }
    }
}
