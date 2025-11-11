namespace MesMiddleware.Monitor.Models;

/// <summary>
/// DTO for connection status from middleware service.
/// </summary>
public class ConnectionStatusDto
{
    public string Status { get; set; } = string.Empty; // Connected, Disconnected, Retrying
    public DateTime LastPingTimestamp { get; set; }
    public int QueueDepth { get; set; }
}
