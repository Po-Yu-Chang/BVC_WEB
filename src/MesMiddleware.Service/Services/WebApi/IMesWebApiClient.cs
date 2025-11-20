using MesMiddleware.Shared.Models;
using MesMiddleware.Service.Controllers;

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
    /// Verifies trace codes belong to specified work order (混批檢測).
    /// Forwards request to MES Cloud trace verification API.
    /// </summary>
    /// <param name="request">Trace verification request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification result</returns>
    Task<TraceVerificationResponse> VerifyTraceCodesAsync(TraceVerificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// 追溯碼校驗回應 DTO
/// </summary>
public class TraceVerificationResponse
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Msg { get; set; }
    public string? Code { get; set; }
}
