using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MesMiddleware.Shared.Models;
using MesMiddleware.Service.Controllers;
using MesMiddleware.Service.Models;
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

            // Prepare request (根據 PDF 文檔第 4 頁規範)
            // URL: POST /CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3
            var request = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3")
            {
                Content = JsonContent.Create(data, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };

            // Add accessToken header (required by MES Cloud API - PDF 規範)
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

    public async Task<TraceVerificationResponse> VerifyTraceCodesAsync(
        TraceVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get authentication token
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            _logger.LogInformation("Calling MES Cloud trace verification API for work order {WorkOrder}, codes count: {Count}",
                request.WoNum, request.Codes?.Count ?? 0);

            // Prepare HTTP request to MES Cloud
            // URL: POST http://{ip}:{port}/CimforceTraceMgrDev/api/transcode/checkcode
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/transcode/checkcode")
            {
                Content = JsonContent.Create(request, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };

            // Add accessToken header (required by MES Cloud API)
            httpRequest.Headers.Add("accessToken", token);

            // Send request
            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            // Handle 401 Unauthorized - token may have expired
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized from MES Cloud, refreshing token and retrying");
                await _tokenService.RefreshTokenAsync(cancellationToken);

                // Retry with new token
                token = await _tokenService.GetAccessTokenAsync(cancellationToken);
                httpRequest.Headers.Remove("accessToken");
                httpRequest.Headers.Add("accessToken", token);

                response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var verificationResponse = JsonSerializer.Deserialize<TraceVerificationResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (verificationResponse == null)
            {
                _logger.LogError("Failed to parse MES Cloud trace verification response");
                return new TraceVerificationResponse
                {
                    Success = false,
                    Msg = "無法解析 MES Cloud API 回應",
                    Code = "500"
                };
            }

            _logger.LogInformation("MES Cloud trace verification result: Success={Success}, Code={Code}, Message={Message}",
                verificationResponse.Success, verificationResponse.Code, verificationResponse.Msg);

            return verificationResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call MES Cloud trace verification API");
            return new TraceVerificationResponse
            {
                Success = false,
                Msg = $"呼叫 MES Cloud API 失敗: {ex.Message}",
                Code = "500"
            };
        }
    }

    private class UploadResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int? Code { get; set; }
    }
}
