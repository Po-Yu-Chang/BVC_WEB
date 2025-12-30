using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Data;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Shared.Models;
using System.Net.Http;
using System.Text.Json;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 上傳佇列服務實作 - 管理 SQLite 佇列和 Hangfire 重試工作
/// </summary>
public class UploadQueueService : IUploadQueueService
{
    private readonly MonitorDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<UploadQueueService> _logger;
    private readonly int _maxRetries;
    private readonly int _retryDelaySeconds;
    private readonly bool _exponentialBackoff;

    public UploadQueueService(
        MonitorDbContext dbContext,
        IBackgroundJobClient backgroundJobClient,
        IConfiguration configuration,
        ILogger<UploadQueueService> logger)
    {
        _dbContext = dbContext;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;

        // 從 appsettings.json 讀取重試配置
        _maxRetries = configuration.GetValue<int>("Queue:MaxRetries", 5);
        _retryDelaySeconds = configuration.GetValue<int>("Queue:RetryDelaySeconds", 2);
        _exponentialBackoff = configuration.GetValue<bool>("Queue:ExponentialBackoff", true);
    }

    public async Task EnqueueAsync(InspectionRecord data, string errorMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("MES Cloud upload failed - saving to SQLite offline queue: TraceCode={TraceCode}, LotNo={LotNo}, DevName={DevName}, Error={Error}",
                data.TraceCode, data.LotNo, data.DevName, errorMessage);

            var queueItem = new UploadQueueItem
            {
                TraceCode = data.TraceCode,
                LotNo = data.LotNo,
                ProcName = data.ProcName,
                DevName = data.DevName,
                UserName = data.UserName,
                WorkClass = data.WorkClass,
                InspectionTime = data.InspectionTime,
                ParamDataJson = data.ParamData != null ? JsonSerializer.Serialize(data.ParamData) : null,
                BenchmarksJson = data.Benchmarks != null ? JsonSerializer.Serialize(data.Benchmarks) : null,
                OtherDataJson = data.OtherData != null ? JsonSerializer.Serialize(data.OtherData) : null,
                CreatedAt = DateTime.UtcNow,
                RetryCount = 0,
                NextRetryAt = DateTime.UtcNow.AddSeconds(_retryDelaySeconds), // 首次重試延遲
                LastError = errorMessage,
                Status = "Pending"
            };

            _dbContext.UploadQueue.Add(queueItem);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("✓ Successfully saved to SQLite queue: {TraceCodeOrLot} (QueueID: {QueueItemId}), will retry at {NextRetryAt}",
                data.TraceCode ?? data.LotNo, queueItem.Id, queueItem.NextRetryAt?.ToLocalTime());

            // 立即排程第一次重試（不等待週期性 job）
            var delay = TimeSpan.FromSeconds(_retryDelaySeconds);
            _backgroundJobClient.Schedule<RetryUploadJob>(
                job => job.ProcessSingleRetryAsync(queueItem.Id, CancellationToken.None),
                delay);

            _logger.LogInformation("Scheduled immediate retry for queue item {QueueItemId} in {Delay}s",
                queueItem.Id, delay.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ CRITICAL: Failed to save data to SQLite queue! TraceCode={TraceCode}, LotNo={LotNo}, DevName={DevName} - DATA LOSS RISK!",
                data.TraceCode, data.LotNo, data.DevName);
        }
    }

    public async Task<List<UploadQueueItem>> GetPendingItemsAsync(int maxItems = 100, CancellationToken cancellationToken = default)
    {
        // 取得所有 Pending 狀態的項目（不再檢查 NextRetryAt，確保所有待處理項目都會被處理）
        return await _dbContext.UploadQueue
            .Where(q => q.Status == "Pending")
            .OrderBy(q => q.CreatedAt)
            .Take(maxItems)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsProcessingAsync(int queueItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _dbContext.UploadQueue.FindAsync(new object[] { queueItemId }, cancellationToken);
            if (item != null)
            {
                item.Status = "Processing";
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Queue item {QueueItemId} marked as Processing", queueItemId);
            }
            else
            {
                _logger.LogWarning("Cannot mark queue item {QueueItemId} as Processing - item not found in database", queueItemId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark queue item {QueueItemId} as Processing", queueItemId);
        }
    }

    public async Task MarkAsSuccessAsync(int queueItemId, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.UploadQueue.FindAsync(new object[] { queueItemId }, cancellationToken);
        if (item != null)
        {
            // 記錄成功歷史
            await AddHistoryAsync(
                item.TraceCode,
                item.LotNo,
                "Success",
                null,
                item.RetryCount,
                "Queue",
                queueItemId,
                cancellationToken);

            // 從佇列中移除
            _dbContext.UploadQueue.Remove(item);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Queue item {QueueItemId} retry successful - removed from queue", queueItemId);
        }
    }

    public async Task MarkAsFailedAsync(int queueItemId, string errorMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _dbContext.UploadQueue.FindAsync(new object[] { queueItemId }, cancellationToken);
            if (item == null)
            {
                _logger.LogWarning("Cannot mark queue item {QueueItemId} as Failed - item not found in database", queueItemId);
                return;
            }

            item.RetryCount++;
            item.LastRetryAt = DateTime.UtcNow;
            item.LastError = errorMessage;

            // 補報機制：無限重試直到成功（無最大次數限制、無時間限制）
            // 計算下次重試時間（固定間隔，不使用指數退避避免間隔過長）
            int delaySeconds = _retryDelaySeconds; // 固定使用初始延遲（預設 2 秒）

            item.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            item.Status = "Pending";

            // 排程下次重試
            var delay = TimeSpan.FromSeconds(delaySeconds);
            _backgroundJobClient.Schedule<RetryUploadJob>(
                job => job.ProcessSingleRetryAsync(queueItemId, CancellationToken.None),
                delay);

            _logger.LogWarning("Queue item {QueueItemId} ({TraceCode}) retry #{RetryCount} failed: {Error} - will retry at {NextRetryAt}",
                queueItemId, item.TraceCode ?? item.LotNo, item.RetryCount, errorMessage, item.NextRetryAt?.ToLocalTime());

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ Failed to update queue item {QueueItemId} status in database - retry may be lost!", queueItemId);
        }
    }

    public async Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var stats = new QueueStatistics
        {
            PendingCount = await _dbContext.UploadQueue.CountAsync(q => q.Status == "Pending", cancellationToken),
            ProcessingCount = await _dbContext.UploadQueue.CountAsync(q => q.Status == "Processing", cancellationToken),
            FailedCount = await _dbContext.UploadQueue.CountAsync(q => q.Status == "Failed", cancellationToken),
            MaxRetriesExceededCount = await _dbContext.UploadQueue.CountAsync(q => q.Status == "MaxRetriesExceeded", cancellationToken),
            TotalQueuedToday = await _dbContext.UploadQueue.CountAsync(q => q.CreatedAt >= today, cancellationToken)
        };

        // Debug log - 顯示所有佇列項目狀態
        var totalCount = await _dbContext.UploadQueue.CountAsync(cancellationToken);
        if (totalCount > 0)
        {
            _logger.LogDebug("Queue stats: Total={Total}, Pending={Pending}, Processing={Processing}, Failed={Failed}",
                totalCount, stats.PendingCount, stats.ProcessingCount, stats.FailedCount);
        }

        return stats;
    }


    public async Task AddHistoryAsync(
        string? traceCode,
        string? lotNo,
        string status,
        string? errorMessage,
        int retryCount,
        string source,
        int? queueItemId = null,
        CancellationToken cancellationToken = default)
    {
        var history = new UploadHistory
        {
            TraceCode = traceCode,
            LotNo = lotNo,
            UploadedAt = DateTime.UtcNow,
            Status = status,
            ErrorMessage = errorMessage,
            RetryCount = retryCount,
            Source = source,
            QueueItemId = queueItemId
        };

        _dbContext.UploadHistory.Add(history);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.UploadQueue.CountAsync(cancellationToken);
        if (count > 0)
        {
            _dbContext.UploadQueue.RemoveRange(_dbContext.UploadQueue);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleared all {Count} items from upload queue", count);
        }
        return count;
    }
}

/// <summary>
/// Hangfire 批次重試工作 - 週期性處理所有到期的佇列項目
/// </summary>
public class RetryUploadJob
{
    private readonly IUploadQueueService _queueService;
    private readonly ITokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RetryUploadJob> _logger;
    private readonly MonitorDbContext _dbContext;
    private readonly WebApiOptions _webApiOptions;

    public RetryUploadJob(
        IUploadQueueService queueService,
        ITokenService tokenService,
        IHttpClientFactory httpClientFactory,
        ILogger<RetryUploadJob> logger,
        MonitorDbContext dbContext,
        Microsoft.Extensions.Options.IOptions<WebApiOptions> webApiOptions)
    {
        _queueService = queueService;
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _dbContext = dbContext;
        _webApiOptions = webApiOptions.Value;
    }

    /// <summary>
    /// 批次處理所有到期的佇列項目（由 Hangfire RecurringJob 週期性呼叫）
    /// </summary>
    public async Task ProcessPendingQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 取得所有到期的待重試項目
            var pendingItems = await _queueService.GetPendingItemsAsync(maxItems: 100, cancellationToken);

            if (pendingItems.Count == 0)
            {
                // 沒有待處理項目，不輸出日誌（避免日誌過多）
                return;
            }

            _logger.LogInformation("Processing {Count} pending queue items", pendingItems.Count);

            int successCount = 0;
            int failCount = 0;

            foreach (var item in pendingItems)
            {
                var success = await ProcessSingleRetryAsync(item.Id, cancellationToken);
                if (success)
                    successCount++;
                else
                    failCount++;
            }

            _logger.LogInformation("Batch processing completed: {SuccessCount} succeeded, {FailCount} failed",
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch queue processing");
        }
    }

    /// <summary>
    /// 處理單一佇列項目重試（由 Hangfire 排程 job 呼叫）
    /// </summary>
    public async Task<bool> ProcessSingleRetryAsync(int queueItemId, CancellationToken cancellationToken)
    {
        try
        {
            // 取得佇列項目
            var item = await _dbContext.UploadQueue.FindAsync(new object[] { queueItemId }, cancellationToken);
            if (item == null)
            {
                _logger.LogWarning("Queue item {QueueItemId} not found - may have been deleted", queueItemId);
                return false;
            }

            // 檢查狀態
            if (item.Status != "Pending")
            {
                _logger.LogWarning("Queue item {QueueItemId} status is {Status} - skipping", queueItemId, item.Status);
                return false;
            }

            // 標記為處理中
            await _queueService.MarkAsProcessingAsync(queueItemId, cancellationToken);

            // 重建 InspectionRecord
            var data = new InspectionRecord
            {
                TraceCode = item.TraceCode,
                LotNo = item.LotNo,
                ProcName = item.ProcName ?? string.Empty,
                DevName = item.DevName ?? string.Empty,
                UserName = item.UserName ?? string.Empty,
                WorkClass = item.WorkClass ?? string.Empty,
                InspectionTime = item.InspectionTime,
                ParamData = !string.IsNullOrEmpty(item.ParamDataJson)
                    ? JsonSerializer.Deserialize<List<ParamDataItem>>(item.ParamDataJson) ?? new List<ParamDataItem>()
                    : new List<ParamDataItem>(),
                Benchmarks = !string.IsNullOrEmpty(item.BenchmarksJson)
                    ? JsonSerializer.Deserialize<List<BenchmarkItem>>(item.BenchmarksJson) ?? new List<BenchmarkItem>()
                    : new List<BenchmarkItem>(),
                OtherData = !string.IsNullOrEmpty(item.OtherDataJson)
                    ? JsonSerializer.Deserialize<List<OtherDataItem>>(item.OtherDataJson) ?? new List<OtherDataItem>()
                    : new List<OtherDataItem>()
            };

            // 嘗試上傳到 MES Cloud
            var uploadSucceeded = await UploadToMesCloudAsync(data, cancellationToken);

            if (uploadSucceeded)
            {
                await _queueService.MarkAsSuccessAsync(queueItemId, cancellationToken);
                Controllers.StatusController.IncrementSuccessful();
                Controllers.StatusController.AddMonitorToCloudHistory(
                    item.TraceCode ?? item.LotNo ?? "N/A",
                    "Success (Retry)",
                    null
                );
                _logger.LogInformation("Queue item {QueueItemId} ({TraceCode}) retry successful", queueItemId, item.TraceCode);
                return true;
            }
            else
            {
                await _queueService.MarkAsFailedAsync(queueItemId, "MES Cloud upload failed", cancellationToken);
                Controllers.StatusController.AddMonitorToCloudHistory(
                    item.TraceCode ?? item.LotNo ?? "N/A",
                    "Failed (Retry)",
                    "MES Cloud upload failed"
                );
                _logger.LogWarning("Queue item {QueueItemId} ({TraceCode}) retry failed - will retry later", queueItemId, item.TraceCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during retry for queue item {QueueItemId}", queueItemId);
            await _queueService.MarkAsFailedAsync(queueItemId, ex.Message, cancellationToken);
            return false;
        }
    }

    private async Task<bool> UploadToMesCloudAsync(InspectionRecord data, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            // 根據 PDF 文檔規格準備請求 payload
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
                        partNumber = data.PartNumber,
                        remark = data.Remark,
                        paramData = data.ParamData,
                        benchmarks = data.Benchmarks,
                        otherData = data.OtherData
                    }
                }
            };

            var httpClient = _httpClientFactory.CreateClient("MesCloudClient");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3")
            {
                Content = System.Net.Http.Json.JsonContent.Create(requestPayload, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };

            httpRequest.Headers.Add("accessToken", token);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            // Handle 401 Unauthorized
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized from MES Cloud, refreshing token and retrying");
                await _tokenService.RefreshTokenAsync(cancellationToken);
                token = await _tokenService.GetAccessTokenAsync(cancellationToken);

                var retryRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3")
                {
                    Content = System.Net.Http.Json.JsonContent.Create(requestPayload, options: new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    })
                };
                retryRequest.Headers.Add("accessToken", token);

                response = await httpClient.SendAsync(retryRequest, cancellationToken);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during MES Cloud upload in retry job");
            return false;
        }
    }
}
