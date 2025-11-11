using FluentValidation;
using Microsoft.Extensions.Logging;
using MesMiddleware.Shared.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MesMiddleware.Service.Services.WebApi;

/// <summary>
/// WebAPI client implementation with authentication, validation, and error handling.
/// Uses ITokenService for automatic token management.
/// Validates inspection data before upload using FluentValidation.
/// </summary>
public class MesWebApiClient : IMesWebApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenService _tokenService;
    private readonly IValidator<InspectionRecord> _validator;
    private readonly ILogger<MesWebApiClient> _logger;

    public MesWebApiClient(
        IHttpClientFactory httpClientFactory,
        ITokenService tokenService,
        IValidator<InspectionRecord> validator,
        ILogger<MesWebApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("WebApiClient");
        _tokenService = tokenService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<bool> UploadInspectionDataAsync(InspectionRecord data, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate inspection data before uploading
            var validationResult = await _validator.ValidateAsync(data, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                _logger.LogError("Inspection data validation failed: {Errors}", errors);
                throw new ValidationException($"Invalid inspection data: {errors}");
            }

            // Get authentication token
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            // Prepare request
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/inspection/upload")
            {
                Content = JsonContent.Create(data, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };

            request.Headers.Add("accessToken", token);

            _logger.LogInformation("Uploading inspection data for {TraceCodeOrLot} (RowNo: {RowNo})",
                data.TraceCode ?? data.LotNo,
                data.RowNo);

            // Send request
            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Handle 401 Unauthorized - token may have expired
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized, refreshing token and retrying");
                await _tokenService.RefreshTokenAsync(cancellationToken);

                // Retry with new token
                token = await _tokenService.GetAccessTokenAsync(cancellationToken);
                request.Headers.Remove("accessToken");
                request.Headers.Add("accessToken", token);

                response = await _httpClient.SendAsync(request, cancellationToken);
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (response.IsSuccessStatusCode && uploadResponse?.Success == true)
            {
                _logger.LogInformation("Successfully uploaded inspection data for {TraceCodeOrLot}",
                    data.TraceCode ?? data.LotNo);
                return true;
            }

            _logger.LogError("WebAPI upload failed: HTTP {StatusCode}, Success={Success}, Message={Message}",
                (int)response.StatusCode,
                uploadResponse?.Success,
                uploadResponse?.Message);

            return false;
        }
        catch (ValidationException)
        {
            // Re-throw validation exceptions (caller should handle)
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during inspection data upload");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during inspection data upload");
            throw;
        }
    }

    public async Task<bool> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking WebAPI connection");

            // Try to get a token (this validates connectivity + authentication)
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            _logger.LogDebug("WebAPI connection check: OK");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebAPI connection check failed");
            return false;
        }
    }

    /// <summary>
    /// Sends command acknowledgment from equipment back to WebAPI.
    /// Part of User Story 3 (Bidirectional Command & Control) - T063.
    /// </summary>
    public async Task<bool> SendCommandAcknowledgmentAsync(CommandAcknowledgment acknowledgment, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending command acknowledgment to WebAPI: CommandId={CommandId}, Status={Status}",
                acknowledgment.CommandId, acknowledgment.Status);

            // Get access token
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            // POST /api/equipment/command/ack
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/equipment/command/ack");
            request.Headers.Add("Authorization", $"Bearer {token}");

            var jsonContent = JsonSerializer.Serialize(acknowledgment);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Command acknowledgment sent successfully: CommandId={CommandId}",
                    acknowledgment.CommandId);
                return true;
            }

            _logger.LogWarning("Failed to send command acknowledgment: CommandId={CommandId}, StatusCode={StatusCode}",
                acknowledgment.CommandId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command acknowledgment: CommandId={CommandId}",
                acknowledgment.CommandId);
            return false;
        }
    }

    private class UploadResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? Code { get; set; }
    }
}
