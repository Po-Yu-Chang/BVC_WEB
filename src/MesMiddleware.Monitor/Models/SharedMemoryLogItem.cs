namespace MesMiddleware.Monitor.Models;

/// <summary>
/// Shared memory activity log item for monitoring dashboard.
/// </summary>
public class SharedMemoryLogItem
{
    public DateTime Timestamp { get; set; }
    public required string EventType { get; set; } // "DataReceived", "ParseError", "ValidationError"
    public required string Message { get; set; }
    public string? TraceCodeOrLotNo { get; set; }
    public int DataSizeBytes { get; set; }
}
