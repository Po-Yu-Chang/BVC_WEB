using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MesMiddleware.Monitor.Controllers;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;
using MesMiddleware.Shared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

namespace MesMiddleware.Monitor.Services.HostedServices;

/// <summary>
/// Background service that processes inspection data from in-memory channel.
/// Reads from channel and uploads directly to MES Cloud API.
/// Failed uploads are saved to SQLite queue for automatic retry.
/// </summary>
public class InspectionChannelProcessor : BackgroundService
{
    private readonly Channel<InspectionRecord> _inspectionChannel;
    private readonly ITokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InspectionChannelProcessor> _logger;
    private readonly WebApiOptions _webApiOptions;

    public InspectionChannelProcessor(
        Channel<InspectionRecord> inspectionChannel,
        ITokenService tokenService,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<InspectionChannelProcessor> logger,
        IOptions<WebApiOptions> webApiOptions)
    {
        _inspectionChannel = inspectionChannel;
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _webApiOptions = webApiOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inspection channel processor starting...");

        try
        {
            // Check MES Cloud connectivity on startup
            var connectionOk = await CheckMesCloudConnectionAsync(stoppingToken);
            if (connectionOk)
            {
                _logger.LogInformation("MES Cloud connection check: OK");
            }
            else
            {
                _logger.LogWarning("MES Cloud connection check failed - will continue trying to upload");
            }

            // Process channel messages until cancellation
            await foreach (var data in _inspectionChannel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessInspectionDataAsync(data, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Inspection channel processor is stopping (cancellation requested)");
        }
        catch (System.Threading.Channels.ChannelClosedException)
        {
            _logger.LogInformation("Inspection channel processor is stopping (channel closed)");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in inspection channel processor");
            throw;
        }
        finally
        {
            _logger.LogInformation("Inspection channel processor stopped");
        }
    }

    private async Task<bool> CheckMesCloudConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    private async Task ProcessInspectionDataAsync(InspectionRecord data, CancellationToken cancellationToken)
    {
        StatusController.IncrementReceived();
        StatusController.UpdateLastActivity();

        _logger.LogInformation("Processing inspection data: {TraceCodeOrLot}",
            data.TraceCode ?? data.LotNo);

        try
        {
            // Upload to MES Cloud API directly
            var (uploadSucceeded, errorMessage) = await UploadToMesCloudAsync(data, cancellationToken);

            if (uploadSucceeded)
            {
                StatusController.IncrementSuccessful();
                // 記錄到 Monitor → MES Cloud 歷史
                StatusController.AddMonitorToCloudHistory(
                    data.TraceCode ?? data.LotNo ?? "N/A",
                    "Success",
                    null
                );
                // 記錄到資料庫歷史（使用 scope）
                using (var scope = _serviceProvider.CreateScope())
                {
                    var queueService = scope.ServiceProvider.GetRequiredService<IUploadQueueService>();
                    await queueService.AddHistoryAsync(
                        data.TraceCode,
                        data.LotNo,
                        "Success",
                        null,
                        0,
                        "Device",
                        null,
                        cancellationToken);
                }
                _logger.LogInformation("Successfully uploaded inspection data for {TraceCodeOrLot}",
                    data.TraceCode ?? data.LotNo);
            }
            else
            {
                StatusController.IncrementQueued();
                // 記錄到 Monitor → MES Cloud 歷史（失敗）
                StatusController.AddMonitorToCloudHistory(
                    data.TraceCode ?? data.LotNo ?? "N/A",
                    "Queued",
                    "Upload failed - saved to queue for retry"
                );
                // 儲存到 SQLite 佇列等待重試（使用 scope）
                using (var scope = _serviceProvider.CreateScope())
                {
                    var queueService = scope.ServiceProvider.GetRequiredService<IUploadQueueService>();
                    await queueService.EnqueueAsync(data, errorMessage ?? "MES Cloud upload failed", cancellationToken);
                }
                _logger.LogWarning("Failed to upload inspection data for {TraceCodeOrLot} - saved to SQLite queue for retry",
                    data.TraceCode ?? data.LotNo);
            }
        }
        catch (Exception ex)
        {
            StatusController.IncrementQueued();
            // 記錄到 Monitor → MES Cloud 歷史（異常）
            StatusController.AddMonitorToCloudHistory(
                data.TraceCode ?? data.LotNo ?? "N/A",
                "Queued",
                $"Exception: {ex.Message} - saved to queue for retry"
            );
            // 儲存到 SQLite 佇列等待重試（使用 scope）
            using (var scope = _serviceProvider.CreateScope())
            {
                var queueService = scope.ServiceProvider.GetRequiredService<IUploadQueueService>();
                await queueService.EnqueueAsync(data, ex.Message, cancellationToken);
            }
            _logger.LogError(ex, "Failed to upload inspection data for {TraceCodeOrLot} - saved to SQLite queue for retry",
                data.TraceCode ?? data.LotNo);
        }
    }

    private async Task<(bool success, string? errorMessage)> UploadToMesCloudAsync(InspectionRecord data, CancellationToken cancellationToken)
    {
        try
        {
            // Get authentication token
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            // Prepare request payload according to MES Cloud API specification
            var requestPayload = new
            {
                isVerifyLot = false,
                data = new[]
                {
                    new
                    {
                        traceCode = data.TraceCode,
                        lotNo = data.LotNo,
                        procName = data.ProcName,
                        devName = data.DevName,
                        userName = data.UserName,
                        workClass = data.WorkClass,
                        partNumber = (string?)null,
                        remark = (string?)null,
                        paramData = data.ParamData,
                        benchmarks = data.Benchmarks,
                        otherData = data.OtherData
                    }
                }
            };

            var httpClient = _httpClientFactory.CreateClient("MesCloudClient");

            // Call MES Cloud API: POST /CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3")
            {
                Content = JsonContent.Create(requestPayload, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };

            // Add accessToken header (根據 PDF 文檔規格)
            httpRequest.Headers.Add("accessToken", token);

            // Send request
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            // Handle 401 Unauthorized - refresh token and retry
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized from MES Cloud, refreshing token and retrying");
                await _tokenService.RefreshTokenAsync(cancellationToken);

                // Retry with new token
                token = await _tokenService.GetAccessTokenAsync(cancellationToken);

                var retryRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3")
                {
                    Content = JsonContent.Create(requestPayload, options: new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    })
                };
                retryRequest.Headers.Add("accessToken", token);

                response = await httpClient.SendAsync(retryRequest, cancellationToken);
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("MES Cloud upload successful: {StatusCode}", response.StatusCode);
                return (true, null);
            }
            else
            {
                var errorMsg = $"MES Cloud returned {response.StatusCode}: {responseContent}";
                _logger.LogWarning(errorMsg);
                return (false, errorMsg);
            }
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = $"HTTP request failed: {ex.Message}";
            _logger.LogError(ex, "HTTP request exception during MES Cloud upload");
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Exception during MES Cloud upload: {ex.Message}";
            _logger.LogError(ex, errorMsg);
            return (false, errorMsg);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Graceful shutdown initiated - completing channel items...");

        try
        {
            // Complete the channel (no more writes)
            _inspectionChannel.Writer.Complete();

            // Allow base class to trigger ExecuteAsync cancellation
            await base.StopAsync(cancellationToken);

            _logger.LogInformation("Graceful shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception during shutdown (this is expected during application exit)");
        }
    }
}
