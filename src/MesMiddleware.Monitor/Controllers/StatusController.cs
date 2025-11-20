using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Services;
using System.Collections.Concurrent;

namespace MesMiddleware.Monitor.Controllers;

/// <summary>
/// API endpoint for DeviceSimulator to query Monitor status.
/// Provides connection status and statistics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<StatusController> _logger;

    // Static counters for statistics (shared across requests)
    private static int _totalReceived = 0;
    private static int _successfulUploads = 0;
    private static int _queuedUploads = 0;
    private static DateTime _lastActivity = DateTime.UtcNow;

    // Device 連線狀態（最後一次收到資料的時間）
    private static DateTime _lastDeviceActivity = DateTime.MinValue;
    private static readonly TimeSpan DeviceConnectionTimeout = TimeSpan.FromSeconds(10); // 10 秒內沒收到資料視為斷線

    // In-memory history (最多保留 1000 筆)
    // Device -> Monitor 歷史
    private static readonly ConcurrentQueue<HistoryItem> _deviceToMonitorHistory = new();
    // Monitor -> MES Cloud 歷史
    private static readonly ConcurrentQueue<HistoryItem> _monitorToCloudHistory = new();
    private static readonly int MaxHistoryItems = 1000;

    public static void IncrementReceived() => Interlocked.Increment(ref _totalReceived);
    public static void IncrementSuccessful() => Interlocked.Increment(ref _successfulUploads);
    public static void IncrementQueued() => Interlocked.Increment(ref _queuedUploads);
    public static void UpdateLastActivity() => _lastActivity = DateTime.UtcNow;

    /// <summary>
    /// 更新設備活動時間（當收到設備資料時呼叫）
    /// </summary>
    public static void UpdateDeviceActivity() => _lastDeviceActivity = DateTime.UtcNow;

    /// <summary>
    /// 檢查設備是否連線（10 秒內有收到資料）
    /// </summary>
    public static bool IsDeviceConnected() => (DateTime.UtcNow - _lastDeviceActivity) < DeviceConnectionTimeout;

    /// <summary>
    /// 記錄 Device → Monitor 歷史（設備發送資料到 Monitor）
    /// </summary>
    public static void AddDeviceToMonitorHistory(string traceCode, string rowNo, string status, string? errorMessage = null)
    {
        _deviceToMonitorHistory.Enqueue(new HistoryItem
        {
            Timestamp = DateTime.UtcNow,
            TraceCode = traceCode,
            RowNo = rowNo,
            Status = status,
            ErrorMessage = errorMessage,
            RetryCount = 0
        });

        while (_deviceToMonitorHistory.Count > MaxHistoryItems)
        {
            _deviceToMonitorHistory.TryDequeue(out _);
        }
    }

    /// <summary>
    /// 記錄 Monitor → MES Cloud 歷史（Monitor 上傳到雲端）
    /// </summary>
    public static void AddMonitorToCloudHistory(string traceCode, string rowNo, string status, string? errorMessage = null)
    {
        _monitorToCloudHistory.Enqueue(new HistoryItem
        {
            Timestamp = DateTime.UtcNow,
            TraceCode = traceCode,
            RowNo = rowNo,
            Status = status,
            ErrorMessage = errorMessage,
            RetryCount = 0
        });

        while (_monitorToCloudHistory.Count > MaxHistoryItems)
        {
            _monitorToCloudHistory.TryDequeue(out _);
        }
    }

    public StatusController(
        ITokenService tokenService,
        ILogger<StatusController> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Get current Monitor status.
    /// Endpoint: GET /api/status
    /// Used by DeviceSimulator to test connection.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            // 檢查設備是否連線（10 秒內有收到資料）
            var deviceConnected = IsDeviceConnected();

            // 檢查 MES Cloud 連線狀態
            var mesCloudConnected = _tokenService.IsTokenValid();

            // 如果沒有 token，嘗試取得（不阻塞）
            if (!mesCloudConnected)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _tokenService.GetAccessTokenAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore
                    }
                });
            }

            // Device 連線狀態決定整體狀態
            var connectionStatus = deviceConnected ? "Connected" : "Disconnected";

            return Ok(new
            {
                connectionStatus = connectionStatus, // Device → Monitor 連線狀態
                lastActivity = _lastActivity,
                lastDeviceActivity = _lastDeviceActivity, // 最後一次收到設備資料的時間
                statistics = new
                {
                    totalReceived = _totalReceived,
                    successfulUploads = _successfulUploads,
                    queuedUploads = _queuedUploads,
                    currentQueueSize = 0
                },
                timestamp = DateTime.UtcNow,
                mesCloudStatus = mesCloudConnected ? "Connected" : "Disconnected" // MES Cloud 連線狀態
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get Device → Monitor upload history.
    /// Endpoint: GET /api/status/history?limit=100
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHistory([FromQuery] int limit = 100)
    {
        var history = _deviceToMonitorHistory
            .Reverse()
            .Take(limit)
            .Select(h => new
            {
                timestamp = h.Timestamp,
                traceCode = h.TraceCode,
                rowNo = h.RowNo,
                status = h.Status,
                errorMessage = h.ErrorMessage,
                retryCount = h.RetryCount
            })
            .ToList();

        return Ok(history);
    }

    /// <summary>
    /// Get Monitor → MES Cloud upload history.
    /// Endpoint: GET /api/status/upload-history?limit=100
    /// </summary>
    [HttpGet("upload-history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetUploadHistory([FromQuery] int limit = 100)
    {
        var history = _monitorToCloudHistory
            .Reverse()
            .Take(limit)
            .Select(h => new
            {
                timestamp = h.Timestamp,
                traceCode = h.TraceCode,
                rowNo = h.RowNo,
                status = h.Status,
                errorMessage = h.ErrorMessage,
                retryCount = h.RetryCount
            })
            .ToList();

        return Ok(history);
    }

    private class HistoryItem
    {
        public DateTime Timestamp { get; set; }
        public string TraceCode { get; set; } = string.Empty;
        public string RowNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
    }
}
