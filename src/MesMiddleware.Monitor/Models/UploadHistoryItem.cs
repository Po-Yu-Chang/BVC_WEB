namespace MesMiddleware.Monitor.Models;

/// <summary>
/// Upload history item for display in monitoring dashboard.
/// </summary>
public class UploadHistoryItem
{
    public DateTime Timestamp { get; set; }
    public required string TraceCodeOrLotNo { get; set; }
    public required string DeviceName { get; set; }
    public required string Status { get; set; } // "Success", "Queued", "Failed"
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}
