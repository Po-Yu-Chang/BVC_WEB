using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MesMiddleware.Service.Services.Queue;
using MesMiddleware.Service.Services.SharedMemory;
using MesMiddleware.Service.Services.WebApi;

namespace MesMiddleware.Service.Services.HostedServices;

/// <summary>
/// Main hosted service orchestrating middleware operations.
/// Subscribes to SharedMemoryMonitor events, uploads to WebAPI, queues on failure.
/// Implements graceful shutdown with in-flight upload completion.
/// </summary>
public class MiddlewareHostedService : BackgroundService
{
    private readonly ISharedMemoryMonitor _sharedMemoryMonitor;
    private readonly IMesWebApiClient _webApiClient;
    private readonly IUploadQueueService _uploadQueueService;
    private readonly ILogger<MiddlewareHostedService> _logger;

    private int _totalDataReceived = 0;
    private int _successfulUploads = 0;
    private int _queuedUploads = 0;

    public MiddlewareHostedService(
        ISharedMemoryMonitor sharedMemoryMonitor,
        IMesWebApiClient webApiClient,
        IUploadQueueService uploadQueueService,
        ILogger<MiddlewareHostedService> logger)
    {
        _sharedMemoryMonitor = sharedMemoryMonitor;
        _webApiClient = webApiClient;
        _uploadQueueService = uploadQueueService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MES Middleware Service starting...");

        try
        {
            // Check WebAPI connectivity on startup
            var connectionOk = await _webApiClient.CheckConnectionAsync(stoppingToken);
            if (connectionOk)
            {
                _logger.LogInformation("WebAPI connection check: OK");
            }
            else
            {
                _logger.LogWarning("WebAPI connection check failed - will queue uploads until connection restored");
            }

            // Subscribe to shared memory data events
            _sharedMemoryMonitor.DataReceived += OnInspectionDataReceived;

            // Start shared memory monitoring
            await _sharedMemoryMonitor.StartAsync(stoppingToken);

            _logger.LogInformation("MES Middleware Service started successfully");
            _logger.LogInformation("Monitoring shared memory for equipment inspection data...");

            // Keep service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MES Middleware Service is stopping (cancellation requested)");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in MES Middleware Service");
            throw;
        }
        finally
        {
            // Unsubscribe from events
            _sharedMemoryMonitor.DataReceived -= OnInspectionDataReceived;

            // Stop shared memory monitoring
            await _sharedMemoryMonitor.StopAsync();

            _logger.LogInformation("MES Middleware Service stopped");
            _logger.LogInformation("Session statistics: Received={Received}, Uploaded={Uploaded}, Queued={Queued}",
                _totalDataReceived,
                _successfulUploads,
                _queuedUploads);
        }
    }

    /// <summary>
    /// Event handler for inspection data received from shared memory.
    /// Attempts upload to WebAPI, queues on failure with retry logic.
    /// </summary>
    private async void OnInspectionDataReceived(object? sender, InspectionDataReceivedEventArgs e)
    {
        Interlocked.Increment(ref _totalDataReceived);

        _logger.LogInformation("Received inspection data from equipment: {TraceCodeOrLot} (RowNo: {RowNo}, Size: {Size} bytes)",
            e.Data.TraceCode ?? e.Data.LotNo,
            e.Data.RowNo,
            e.DataSizeBytes);

        try
        {
            // Attempt upload to WebAPI
            var uploadSucceeded = await _webApiClient.UploadInspectionDataAsync(e.Data);

            if (uploadSucceeded)
            {
                Interlocked.Increment(ref _successfulUploads);
                _logger.LogInformation("Successfully uploaded inspection data for {TraceCodeOrLot}",
                    e.Data.TraceCode ?? e.Data.LotNo);
            }
            else
            {
                // WebAPI returned failure response - queue for retry
                await QueueFailedUploadAsync(e.Data, "WebAPI returned failure response");
            }
        }
        catch (Exception ex)
        {
            // Upload failed (network error, timeout, etc.) - queue for retry
            _logger.LogError(ex, "Failed to upload inspection data for {TraceCodeOrLot}",
                e.Data.TraceCode ?? e.Data.LotNo);

            await QueueFailedUploadAsync(e.Data, ex.Message);
        }
    }

    private async Task QueueFailedUploadAsync(Shared.Models.InspectionRecord data, string errorMessage)
    {
        try
        {
            var queueId = await _uploadQueueService.QueueUploadAsync(data, errorMessage);
            Interlocked.Increment(ref _queuedUploads);

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
        _logger.LogInformation("Graceful shutdown initiated...");

        // Allow base class to trigger ExecuteAsync cancellation
        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Graceful shutdown completed");
    }
}
