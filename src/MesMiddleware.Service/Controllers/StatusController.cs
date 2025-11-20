using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MesMiddleware.Service.Data;
using MesMiddleware.Service.Services.Queue;
using MesMiddleware.Service.Services.WebApi;

namespace MesMiddleware.Service.Controllers;

/// <summary>
/// API endpoint for WPF Monitor application to query service status.
/// Supports polling-based monitoring.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IUploadQueueService _queueService;
    private readonly IMesWebApiClient _webApiClient;
    private readonly MiddlewareDbContext _dbContext;
    private readonly ILogger<StatusController> _logger;

    // Static counters for statistics (shared across requests)
    private static int _totalReceived = 0;
    private static int _successfulUploads = 0;
    private static int _queuedUploads = 0;
    private static DateTime _lastActivity = DateTime.UtcNow;
    private static string _connectionStatus = "Unknown";

    public static void IncrementReceived() => Interlocked.Increment(ref _totalReceived);
    public static void IncrementSuccessful() => Interlocked.Increment(ref _successfulUploads);
    public static void IncrementQueued() => Interlocked.Increment(ref _queuedUploads);
    public static void UpdateLastActivity() => _lastActivity = DateTime.UtcNow;
    public static void UpdateConnectionStatus(string status) => _connectionStatus = status;

    public StatusController(
        IUploadQueueService queueService,
        IMesWebApiClient webApiClient,
        MiddlewareDbContext dbContext,
        ILogger<StatusController> logger)
    {
        _queueService = queueService;
        _webApiClient = webApiClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get current service status for monitoring.
    /// Endpoint: GET /api/status
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            // Check WebAPI connectivity
            var isConnected = await _webApiClient.CheckConnectionAsync(CancellationToken.None);

            return Ok(new
            {
                connectionStatus = isConnected ? "Connected" : "Disconnected",
                lastActivity = _lastActivity,
                statistics = new
                {
                    totalReceived = _totalReceived,
                    successfulUploads = _successfulUploads,
                    queuedUploads = _queuedUploads,
                    currentQueueSize = await GetQueueSizeAsync()
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get upload history for monitoring (Equipment → Middleware queue).
    /// Endpoint: GET /api/status/history?limit=100
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 100)
    {
        try
        {
            var history = await _queueService.GetRecentHistoryAsync(limit);

            return Ok(history.Select(h => new
            {
                timestamp = h.CreatedAt,
                traceCode = h.TraceCode,
                rowNo = h.RowNo,
                status = h.Status,
                errorMessage = h.LastErrorMessage,
                retryCount = h.RetryCount
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get history");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get MES Cloud upload history (Middleware → MES Cloud).
    /// Endpoint: GET /api/status/upload-history?limit=100
    /// </summary>
    [HttpGet("upload-history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUploadHistory([FromQuery] int limit = 100)
    {
        try
        {
            var history = await _dbContext.UploadHistory
                .OrderByDescending(h => h.UploadedAt)
                .Take(limit)
                .Select(h => new
                {
                    id = h.Id,
                    uploadedAt = h.UploadedAt,
                    machineNumber = h.MachineNumber,
                    traceCode = h.TraceCode,
                    lotNo = h.LotNo,
                    partNumber = h.PartNumber,
                    processName = h.ProcessName,
                    deviceName = h.DeviceName,
                    status = h.Status,
                    responseMessage = h.ResponseMessage,
                    responseCode = h.ResponseCode,
                    errorMessage = h.ErrorMessage,
                    retryCount = h.RetryCount,
                    source = h.Source
                })
                .ToListAsync();

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MES upload history");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Manually retry a failed upload.
    /// Endpoint: POST /api/status/queue/{id}/retry
    /// </summary>
    [HttpPost("queue/{id}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryUpload(int id)
    {
        try
        {
            var success = await _queueService.RetryUploadAsync(id);

            if (!success)
            {
                return NotFound(new { message = "Queue entry not found or already processed" });
            }

            _logger.LogInformation("Manual retry triggered for queue entry {QueueId}", id);

            return Ok(new { message = "Retry initiated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry upload {QueueId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<int> GetQueueSizeAsync()
    {
        try
        {
            var history = await _queueService.GetRecentHistoryAsync(10000);
            return history.Count(h => h.Status == "Pending" || h.Status == "Retrying");
        }
        catch
        {
            return 0;
        }
    }
}
