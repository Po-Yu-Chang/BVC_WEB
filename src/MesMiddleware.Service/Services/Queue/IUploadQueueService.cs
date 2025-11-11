using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Services.Queue;

/// <summary>
/// Service for managing offline upload queue with retry logic.
/// Queues failed uploads to SQLite, processes with Hangfire background jobs.
/// </summary>
public interface IUploadQueueService
{
    /// <summary>
    /// Queues inspection data for later upload (WebAPI unavailable).
    /// Creates QueuedUpload record in SQLite and schedules Hangfire retry job.
    /// </summary>
    /// <param name="data">Inspection data to queue</param>
    /// <param name="errorMessage">Reason for queuing (e.g., HTTP error)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queue entry ID</returns>
    Task<Guid> QueueUploadAsync(InspectionRecord data, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a queued upload (called by Hangfire background job).
    /// If successful, removes from queue. If fails, reschedules with exponential backoff.
    /// </summary>
    /// <param name="queueId">Queue entry ID to retry</param>
    /// <returns>True if upload succeeded, false if retry scheduled</returns>
    Task<bool> RetryQueuedUploadAsync(Guid queueId);

    /// <summary>
    /// Gets count of pending/failed uploads in queue.
    /// </summary>
    Task<int> GetQueueDepthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all queued uploads (for monitoring UI).
    /// </summary>
    Task<List<QueuedUploadInfo>> GetQueuedUploadsAsync(int limit = 100, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for queued upload information (returned to monitoring UI).
/// </summary>
public class QueuedUploadInfo
{
    public Guid Id { get; set; }
    public string TraceCodeOrLotNo { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LastError { get; set; }
}
