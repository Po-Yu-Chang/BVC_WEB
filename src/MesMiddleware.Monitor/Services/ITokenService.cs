namespace MesMiddleware.Monitor.Services;

/// <summary>
/// Service for managing authentication tokens for MES Cloud WebAPI communication.
/// Handles token acquisition, caching, and automatic refresh.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Gets a valid access token, refreshing if necessary.
    /// Returns cached token if still valid, otherwise requests new token from MES Cloud.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Valid access token for MES Cloud authentication</returns>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces token refresh (invalidates cached token).
    /// Use when receiving 401 Unauthorized from MES Cloud.
    /// </summary>
    Task RefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if current cached token is still valid.
    /// </summary>
    bool IsTokenValid();

    /// <summary>
    /// 檢查 MES Cloud 是否實際連線（不只是 Token 是否有效）
    /// </summary>
    bool IsMesCloudConnected();

    /// <summary>
    /// 設定 MES Cloud 連線狀態（供上傳服務呼叫）
    /// </summary>
    void SetMesCloudConnectionStatus(bool connected);
}
