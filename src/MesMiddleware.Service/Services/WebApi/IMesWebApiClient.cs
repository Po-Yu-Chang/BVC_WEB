using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Services.WebApi;

/// <summary>
/// Client for communicating with MES WebAPI service.
/// Handles inspection data uploads with authentication and retry logic.
/// </summary>
public interface IMesWebApiClient
{
    /// <summary>
    /// Uploads inspection data to WebAPI.
    /// Automatically handles authentication token injection.
    /// Throws HttpRequestException on failure (caller should handle retry/queue).
    /// </summary>
    /// <param name="data">Inspection record to upload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if upload succeeded, false otherwise</returns>
    Task<bool> UploadInspectionDataAsync(InspectionRecord data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks WebAPI connectivity and authentication status.
    /// Returns true if WebAPI is reachable and authentication works.
    /// </summary>
    Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends command acknowledgment from equipment back to WebAPI.
    /// Part of User Story 3 (Bidirectional Command & Control).
    /// </summary>
    /// <param name="acknowledgment">Command acknowledgment from equipment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if acknowledgment was successfully sent, false otherwise</returns>
    Task<bool> SendCommandAcknowledgmentAsync(CommandAcknowledgment acknowledgment, CancellationToken cancellationToken = default);
}
