namespace MesMiddleware.Monitor.Models;

/// <summary>
/// Upload queue status for monitoring dashboard.
/// </summary>
public class QueueStatus
{
    public int PendingCount { get; set; }
    public int RetryingCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalDepth { get; set; }
    public DateTime? OldestQueuedAt { get; set; }
}
