namespace MesMiddleware.Monitor.Models;

/// <summary>
/// Represents a shared memory activity log entry.
/// </summary>
public class SharedMemoryActivity
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty; // Read, Write
    public long DataSize { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
}
