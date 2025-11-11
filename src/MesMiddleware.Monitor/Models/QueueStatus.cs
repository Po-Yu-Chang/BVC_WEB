namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 上傳佇列狀態，用於監控儀表板顯示
/// </summary>
public class QueueStatus
{
    /// <summary>
    /// 等待中的項目數量
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// 重試中的項目數量
    /// </summary>
    public int RetryingCount { get; set; }

    /// <summary>
    /// 失敗的項目數量
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 佇列總深度
    /// </summary>
    public int TotalDepth { get; set; }

    /// <summary>
    /// 最舊的排隊時間
    /// </summary>
    public DateTime? OldestQueuedAt { get; set; }
}
