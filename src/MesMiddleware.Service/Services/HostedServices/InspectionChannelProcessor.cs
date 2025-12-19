using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MesMiddleware.Service.Controllers;
using MesMiddleware.Service.Data;
using MesMiddleware.Service.Models;
using MesMiddleware.Service.Services.Queue;
using MesMiddleware.Service.Services.WebApi;
using MesMiddleware.Shared.Models;
using System.Text.Json;
using System.Threading.Channels;

namespace MesMiddleware.Service.Services.HostedServices;

/// <summary>
/// Background service that processes inspection data from in-memory channel.
/// Replaces SharedMemoryMonitor event handling with Channel-based processing.
/// Uploads to WebAPI or queues to SQLite on failure.
/// </summary>
public class InspectionChannelProcessor : BackgroundService
{
    private readonly Channel<InspectionRecord> _inspectionChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InspectionChannelProcessor> _logger;
    private readonly WebApiOptions _webApiOptions;

    public InspectionChannelProcessor(
        Channel<InspectionRecord> inspectionChannel,
        IServiceProvider serviceProvider,
        ILogger<InspectionChannelProcessor> logger,
        IOptions<WebApiOptions> webApiOptions)
    {
        _inspectionChannel = inspectionChannel;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _webApiOptions = webApiOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inspection channel processor starting...");

        try
        {
            // Check WebAPI connectivity on startup
            using (var scope = _serviceProvider.CreateScope())
            {
                var webApiClient = scope.ServiceProvider.GetRequiredService<IMesWebApiClient>();
                var connectionOk = await webApiClient.CheckConnectionAsync(stoppingToken);
                if (connectionOk)
                {
                    _logger.LogInformation("WebAPI connection check: OK");
                    StatusController.UpdateConnectionStatus("Connected");
                }
                else
                {
                    _logger.LogWarning("WebAPI connection check failed - will queue uploads until connection restored");
                    StatusController.UpdateConnectionStatus("Disconnected");
                }
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

    private async Task ProcessInspectionDataAsync(InspectionRecord data, CancellationToken cancellationToken)
    {
        StatusController.IncrementReceived();
        StatusController.UpdateLastActivity();

        _logger.LogInformation("Processing inspection data: {TraceCodeOrLot}",
            data.TraceCode ?? data.LotNo);

        try
        {
            // Attempt upload to WebAPI
            using var scope = _serviceProvider.CreateScope();
            var webApiClient = scope.ServiceProvider.GetRequiredService<IMesWebApiClient>();

            var uploadSucceeded = await webApiClient.UploadInspectionDataAsync(data);

            if (uploadSucceeded)
            {
                StatusController.IncrementSuccessful();
                StatusController.UpdateConnectionStatus("Connected");

                _logger.LogInformation("Successfully uploaded inspection data for {TraceCodeOrLot}",
                    data.TraceCode ?? data.LotNo);

                // Record successful upload to history
                await RecordUploadHistoryAsync(data, "Success", "上傳成功", 200, null, 0, "Equipment", cancellationToken);
            }
            else
            {
                // WebAPI returned failure response - queue for retry
                await QueueFailedUploadAsync(data, "WebAPI returned failure response");
                StatusController.UpdateConnectionStatus("Retrying");

                // Record failed upload to history
                await RecordUploadHistoryAsync(data, "Failed", "WebAPI returned failure response", null, "WebAPI returned failure response", 0, "Equipment", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Upload failed (network error, timeout, etc.) - queue for retry
            _logger.LogError(ex, "Failed to upload inspection data for {TraceCodeOrLot}",
                data.TraceCode ?? data.LotNo);

            await QueueFailedUploadAsync(data, ex.Message);
            StatusController.UpdateConnectionStatus("Disconnected");

            // Record failed upload to history
            await RecordUploadHistoryAsync(data, "Failed", null, null, ex.Message, 0, "Equipment", cancellationToken);
        }
    }

    private async Task RecordUploadHistoryAsync(
        InspectionRecord data,
        string status,
        string? responseMessage,
        int? responseCode,
        string? errorMessage,
        int retryCount,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MiddlewareDbContext>();

            var history = new UploadHistory
            {
                UploadedAt = DateTime.UtcNow,
                MachineNumber = _webApiOptions.MachineNumber,
                TraceCode = data.TraceCode,
                LotNo = data.LotNo,
                PartNumber = null, // Not available in InspectionRecord
                ProcessName = data.ProcName,
                DeviceName = data.DevName,
                Status = status,
                ResponseMessage = responseMessage,
                ResponseCode = responseCode,
                ErrorMessage = errorMessage,
                RetryCount = retryCount,
                Source = source,
                InspectionDataJson = JsonSerializer.Serialize(data) // 保留完整 JSON 用於審計
            };

            dbContext.UploadHistory.Add(history);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't fail the main process if history logging fails
            _logger.LogWarning(ex, "Failed to record upload history for {TraceCodeOrLot}", data.TraceCode ?? data.LotNo);
        }
    }

    private async Task QueueFailedUploadAsync(InspectionRecord data, string errorMessage)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var queueService = scope.ServiceProvider.GetRequiredService<IUploadQueueService>();

            var queueId = await queueService.QueueUploadAsync(data, errorMessage);
            StatusController.IncrementQueued();

            _logger.LogWarning("Queued upload for later retry: {TraceCodeOrLot} (Queue ID: {QueueId})",
                data.TraceCode ?? data.LotNo,
                queueId);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "CRITICAL: Failed to queue upload for {TraceCodeOrLot} - DATA MAY BE LOST",
                data.TraceCode ?? data.LotNo);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Graceful shutdown initiated - completing channel items...");

        // Complete the channel (no more writes)
        _inspectionChannel.Writer.Complete();

        // Allow base class to trigger ExecuteAsync cancellation
        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Graceful shutdown completed");
    }
}
