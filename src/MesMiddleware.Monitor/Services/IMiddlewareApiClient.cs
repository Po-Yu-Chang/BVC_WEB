using MesMiddleware.Monitor.Models;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// Client for communicating with the MES Middleware Service.
/// Retrieves service status, upload history, and queue information.
/// </summary>
public interface IMiddlewareApiClient
{
    /// <summary>
    /// Gets current connection status (Connected/Disconnected/Retrying).
    /// </summary>
    Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent upload history (successful and failed uploads).
    /// </summary>
    Task<List<UploadRecord>> GetUploadHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets shared memory activity log (recent data received events).
    /// </summary>
    Task<List<SharedMemoryActivity>> GetSharedMemoryActivityAsync(int maxRecords = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command history (equipment commands sent via shared memory).
    /// Part of User Story 3 (Bidirectional Command & Control).
    /// </summary>
    Task<List<CommandRecord>> GetCommandHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default);
}
