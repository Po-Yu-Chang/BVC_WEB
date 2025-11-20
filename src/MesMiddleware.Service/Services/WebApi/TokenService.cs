using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MesMiddleware.Service.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace MesMiddleware.Service.Services.WebApi;

/// <summary>
/// Token service implementation with in-memory caching and automatic refresh.
/// Caches token for 8 hours (typical WebAPI token lifetime).
/// Thread-safe using SemaphoreSlim for token refresh synchronization.
/// **Token 儲存在靜態變數中，讓所有 HTTP 請求共享同一個 Token**
/// </summary>
public class TokenService : ITokenService
{
    private readonly HttpClient _httpClient;
    private readonly WebApiOptions _options;
    private readonly ILogger<TokenService> _logger;

    // 靜態變數：Token 全局共享，所有服務實例共用
    private static string? _cachedToken;
    private static DateTime _tokenExpiresAt = DateTime.MinValue;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    public TokenService(
        IHttpClientFactory httpClientFactory,
        IOptions<WebApiOptions> options,
        ILogger<TokenService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("WebApiClient");
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        // Return cached token if still valid
        if (IsTokenValid() && !string.IsNullOrEmpty(_cachedToken))
        {
            _logger.LogDebug("Returning cached access token (expires at {ExpiresAt})", _tokenExpiresAt);
            return _cachedToken;
        }

        // Acquire lock to prevent multiple concurrent token requests
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock (another thread may have refreshed)
            if (IsTokenValid() && !string.IsNullOrEmpty(_cachedToken))
            {
                return _cachedToken;
            }

            _logger.LogInformation("Requesting new access token from MES Cloud for machine {MachineNumber}",
                _options.MachineNumber);

            // Request new token from MES Cloud (根據 PDF 文檔規範)
            // URL: POST /CimforceTraceMgrDev/api/prtmac/prtmacuserlogin
            var loginRequest = new
            {
                PrtMacNo = _options.MachineNumber  // 注意：大寫 P, M, N (根據 PDF 規範)
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/prtmac/prtmacuserlogin")
            {
                Content = JsonContent.Create(loginRequest)
            };

            // Add Referrer header (required by MES Cloud API - PDF 規範)
            request.Headers.Add("Referrer", _options.MachineIp);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (loginResponse?.Success != true || string.IsNullOrEmpty(loginResponse.Token))
            {
                throw new InvalidOperationException($"MES Cloud login failed: {loginResponse?.Msg ?? "Unknown error"}");
            }

            _cachedToken = loginResponse.Token;  // 注意：使用 Token 欄位，不是 AccessToken
            _tokenExpiresAt = DateTime.UtcNow.AddHours(8); // Token 長期有效 (根據 PDF 文檔)

            _logger.LogInformation("Successfully obtained access token from MES Cloud (expires at {ExpiresAt})", _tokenExpiresAt);

            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain access token from WebAPI");
            throw;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Force refreshing access token");
        _cachedToken = null;
        _tokenExpiresAt = DateTime.MinValue;
        await GetAccessTokenAsync(cancellationToken);
    }

    public bool IsTokenValid()
    {
        if (string.IsNullOrEmpty(_cachedToken))
        {
            return false;
        }

        // Consider token invalid if it expires within next 5 minutes (buffer for safety)
        return _tokenExpiresAt > DateTime.UtcNow.AddMinutes(5);
    }

    /// <summary>
    /// MES Cloud 登錄 API 回應 DTO (根據 PDF 文檔第 3 頁)
    /// </summary>
    private class TokenResponse
    {
        public bool Success { get; set; }
        public TokenData? Data { get; set; }
        public string? Msg { get; set; }
        public string? Code { get; set; }

        // 便捷屬性：直接存取 Token
        public string? Token => Data?.Token;
    }

    private class TokenData
    {
        public string? PrtMacNo { get; set; }
        public string? IpAddr { get; set; }
        public string? Token { get; set; }
        public string? SysUserId { get; set; }
    }
}
