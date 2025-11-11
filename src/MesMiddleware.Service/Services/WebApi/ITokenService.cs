namespace MesMiddleware.Service.Services.WebApi;

/// <summary>
/// Service for managing authentication tokens for WebAPI communication.
/// Handles token acquisition, caching, and automatic refresh.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Gets a valid access token, refreshing if necessary.
    /// Returns cached token if still valid, otherwise requests new token from WebAPI.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Valid access token for WebAPI authentication</returns>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces token refresh (invalidates cached token).
    /// Use when receiving 401 Unauthorized from WebAPI.
    /// </summary>
    Task RefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if current cached token is still valid.
    /// </summary>
    bool IsTokenValid();
}
