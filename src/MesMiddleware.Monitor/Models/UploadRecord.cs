namespace MesMiddleware.Monitor.Models;

/// <summary>
/// Represents a single upload record for display in the history view.
/// Maps to QueuedUpload entity in the service database.
/// </summary>
public class UploadRecord
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string TraceCode { get; set; } = string.Empty;
    public string EquipmentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Success, Failed, Pending
    public string? ErrorMessage { get; set; }
}
