using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Services.SharedMemory;

/// <summary>
/// Service for monitoring Windows shared memory for equipment inspection data.
/// Detects data changes via EventWaitHandle and reads JSON from MemoryMappedFile.
/// </summary>
public interface ISharedMemoryMonitor
{
    /// <summary>
    /// Event raised when new inspection data is received from equipment.
    /// Subscribers should handle upload to WebAPI and queue on failure.
    /// </summary>
    event EventHandler<InspectionDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Starts monitoring shared memory for equipment data.
    /// Runs until cancellation token is triggered.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops monitoring shared memory.
    /// </summary>
    Task StopAsync();
}

/// <summary>
/// Event args for inspection data received from shared memory.
/// </summary>
public class InspectionDataReceivedEventArgs : EventArgs
{
    public required InspectionRecord Data { get; init; }
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
    public int DataSizeBytes { get; init; }
}
