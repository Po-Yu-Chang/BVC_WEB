using MesMiddleware.Monitor.Models;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 上傳佇列服務介面 - 管理失敗上傳的 SQLite 佇列
/// </summary>
public interface IUploadQueueService
{
    /// <summary>
    /// 將失敗的上傳加入佇列
    /// </summary>
    Task EnqueueAsync(InspectionRecord data, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得待重試的佇列項目（狀態為 Pending 且 NextRetryAt <= now）
    /// </summary>
    Task<List<UploadQueueItem>> GetPendingItemsAsync(int maxItems = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 標記佇列項目為處理中
    /// </summary>
    Task MarkAsProcessingAsync(int queueItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 標記佇列項目重試成功（從佇列中移除）
    /// </summary>
    Task MarkAsSuccessAsync(int queueItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 標記佇列項目重試失敗（增加重試計數，計算下次重試時間）
    /// </summary>
    Task MarkAsFailedAsync(int queueItemId, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得佇列統計資訊
    /// </summary>
    Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 記錄上傳歷史
    /// </summary>
    Task AddHistoryAsync(string? traceCode, string? lotNo, string status, string? errorMessage, int retryCount, string source, int? queueItemId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除所有佇列項目
    /// </summary>
    Task<int> ClearAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 佇列統計資訊
/// </summary>
public class QueueStatistics
{
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int FailedCount { get; set; }
    public int MaxRetriesExceededCount { get; set; }
    public int TotalQueuedToday { get; set; }
}
