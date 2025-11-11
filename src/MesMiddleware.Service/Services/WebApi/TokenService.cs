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
/// </summary>
public class TokenService : ITokenService
{
    private readonly HttpClient _httpClient;
    private readonly WebApiOptions _options;
    private readonly ILogger<TokenService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

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

            _logger.LogInformation("Requesting new access token from WebAPI for machine {MachineNumber}",
                _options.MachineNumber);

            // Request new token from WebAPI
            var loginRequest = new
            {
                machineNumber = _options.MachineNumber,
                machineIp = _options.MachineIp
            };

            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var loginResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (loginResponse?.Success != true || string.IsNullOrEmpty(loginResponse.AccessToken))
            {
                throw new InvalidOperationException($"WebAPI login failed: {loginResponse?.Message ?? "Unknown error"}");
            }

            _cachedToken = loginResponse.AccessToken;
            _tokenExpiresAt = DateTime.UtcNow.AddHours(8); // Token lifetime from WebAPI spec

            _logger.LogInformation("Successfully obtained access token (expires at {ExpiresAt})", _tokenExpiresAt);

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

    private class TokenResponse
    {
        public bool Success { get; set; }
        public string? AccessToken { get; set; }
        public string? Message { get; set; }
    }
}
