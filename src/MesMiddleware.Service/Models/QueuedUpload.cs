namespace MesMiddleware.Service.Models;

/// <summary>
/// EF Core entity representing a failed/queued upload attempt.
/// Stored in SQLite database for retry processing with Hangfire.
/// </summary>
public class QueuedUpload
{
    /// <summary>
    /// Primary key (GUID for distributed traceability)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Original inspection data as JSON string (preserved for retry)
    /// </summary>
    public required string InspectionDataJson { get; set; }

    /// <summary>
    /// When this upload was first queued (UTC)
    /// </summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of retry attempts made so far
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// When the next retry should be attempted (UTC)
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// Last error message from failed upload attempt
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Last attempted upload time (UTC)
    /// </summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    /// Status of this queued upload (Pending, Retrying, Failed, Completed)
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Machine number from which this data originated
    /// </summary>
    public string? MachineNumber { get; set; }

    /// <summary>
    /// Trace code or lot number for easier lookup
    /// </summary>
    public string? TraceCodeOrLotNo { get; set; }
}
