namespace MesMiddleware.Monitor.Models;

/// <summary>
/// Service status information for monitoring dashboard.
/// </summary>
public class ServiceStatus
{
    public bool IsRunning { get; set; }
    public bool IsWebApiConnected { get; set; }
    public bool IsSharedMemoryActive { get; set; }
    public int TotalDataReceived { get; set; }
    public int SuccessfulUploads { get; set; }
    public int QueuedUploads { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public string? LastError { get; set; }
    public TimeSpan Uptime { get; set; }
}
