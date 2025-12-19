using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Shared.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 直接與 MES Cloud API 通訊的客戶端
/// 負責追溯碼驗證、Token 管理等功能
/// </summary>
public class MiddlewareApiClient : IMiddlewareApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenService _tokenService;
    private readonly ILogger<MiddlewareApiClient> _logger;
    private readonly string _baseUrl;

    /// <summary>
    /// 建構函式,初始化 MES Cloud API 客戶端
    /// </summary>
    /// <param name="baseUrl">MES Cloud API 基礎 URL (例如: http://192.168.1.100:8080)</param>
    /// <param name="httpClient">HTTP 客戶端</param>
    /// <param name="tokenService">Token 服務</param>
    /// <param name="logger">日誌記錄器</param>
    public MiddlewareApiClient(string baseUrl, HttpClient httpClient, ITokenService tokenService, ILogger<MiddlewareApiClient> logger)
    {
        _baseUrl = baseUrl;
        _httpClient = httpClient;
        _tokenService = tokenService;
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    /// <summary>
    /// 取得連線狀態 (呼叫 Monitor 自己的 /api/status 端點)
    /// </summary>
    public async Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking Monitor API status");

            // 呼叫 Monitor 自己的 API: GET http://localhost:5100/api/status
            using var localHttpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5100"),
                Timeout = TimeSpan.FromSeconds(5)
            };

            var response = await localHttpClient.GetAsync("/api/status", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Monitor API status check failed: {StatusCode}", response.StatusCode);
                return new ConnectionStatusDto
                {
                    Status = "Disconnected",
                    LastPingTimestamp = DateTime.UtcNow,
                    QueueDepth = 0
                };
            }

            var statusResponse = await response.Content.ReadFromJsonAsync<StatusApiResponse>(cancellationToken: cancellationToken);

            if (statusResponse == null)
            {
                return new ConnectionStatusDto
                {
                    Status = "Error",
                    LastPingTimestamp = DateTime.UtcNow,
                    QueueDepth = 0
                };
            }

            _logger.LogDebug("Monitor API status check: OK - Device={ConnectionStatus}, MesCloud={MesCloudStatus}",
                statusResponse.ConnectionStatus, statusResponse.MesCloudStatus);
            return new ConnectionStatusDto
            {
                Status = statusResponse.ConnectionStatus,
                LastPingTimestamp = statusResponse.LastActivity,
                QueueDepth = statusResponse.Statistics?.CurrentQueueSize ?? 0,
                TotalReceived = statusResponse.Statistics?.TotalReceived ?? 0,
                SuccessfulUploads = statusResponse.Statistics?.SuccessfulUploads ?? 0,
                QueuedUploads = statusResponse.Statistics?.QueuedUploads ?? 0,
                MesCloudStatus = statusResponse.MesCloudStatus ?? "Disconnected"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed - Monitor API may not be running");
            return new ConnectionStatusDto
            {
                Status = "Disconnected",
                LastPingTimestamp = DateTime.UtcNow,
                QueueDepth = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check Monitor API status");
            return new ConnectionStatusDto
            {
                Status = "Error",
                LastPingTimestamp = DateTime.UtcNow,
                QueueDepth = 0
            };
        }
    }

    /// <summary>
    /// 取得上傳歷史記錄 (設備 → Monitor 佇列，呼叫 Monitor 的 /api/status/history)
    /// </summary>
    public async Task<List<UploadRecord>> GetUploadHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            // 呼叫 Monitor 自己的 API: GET http://localhost:5100/api/status/history?limit={maxRecords}
            using var localHttpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5100"),
                Timeout = TimeSpan.FromSeconds(5)
            };

            var response = await localHttpClient.GetAsync($"/api/status/history?limit={maxRecords}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get upload history from Monitor API: {StatusCode}", response.StatusCode);
                return new List<UploadRecord>();
            }

            var historyItems = await response.Content.ReadFromJsonAsync<List<HistoryApiItem>>(cancellationToken: cancellationToken);

            if (historyItems == null)
            {
                return new List<UploadRecord>();
            }

            return historyItems.Select(item => new UploadRecord
            {
                Id = Guid.NewGuid(), // Generate GUID since API returns int ID
                Timestamp = item.Timestamp,
                TraceCode = item.TraceCode,
                EquipmentName = string.Empty,
                Status = item.Status,
                ErrorMessage = item.ErrorMessage
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get upload history");
            return new List<UploadRecord>();
        }
    }

    /// <summary>
    /// 取得 MES Cloud 上傳歷史記錄 (中介軟體 → MES Cloud，呼叫 Monitor 的 /api/status/upload-history)
    /// </summary>
    public async Task<List<UploadRecord>> GetMesUploadHistoryAsync(int maxRecords = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            // 呼叫 Monitor 自己的 API: GET http://localhost:5100/api/status/upload-history?limit={maxRecords}
            using var localHttpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5100"),
                Timeout = TimeSpan.FromSeconds(5)
            };

            var response = await localHttpClient.GetAsync($"/api/status/upload-history?limit={maxRecords}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get MES upload history from Monitor API: {StatusCode}", response.StatusCode);
                return new List<UploadRecord>();
            }

            // 使用 HistoryApiItem（與 Device->Monitor 相同格式）
            var historyItems = await response.Content.ReadFromJsonAsync<List<HistoryApiItem>>(cancellationToken: cancellationToken);

            if (historyItems == null)
            {
                return new List<UploadRecord>();
            }

            return historyItems.Select(item => new UploadRecord
            {
                Id = Guid.NewGuid(),
                Timestamp = item.Timestamp,
                TraceCode = item.TraceCode,
                EquipmentName = string.Empty,
                Status = item.Status,
                ErrorMessage = item.ErrorMessage
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get MES upload history");
            return new List<UploadRecord>();
        }
    }

    /// <summary>
    /// 手動重試失敗的上傳
    /// </summary>
    public async Task<bool> RetryUploadAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: API expects int ID, need to map Guid to int or change WPF models
            // For now, skip retry functionality (will be implemented when IDs are aligned)
            _logger.LogWarning("Retry functionality not yet mapped to new API - ID conversion needed");
            await Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry upload");
            return false;
        }
    }

    /// <summary>
    /// 驗證追溯碼是否屬於指定工單 (追溯碼校驗 - 混批檢測)
    /// 直接呼叫 MES Cloud API: POST /CimforceTraceMgrDev/api/transcode/checkcode
    /// </summary>
    public async Task<TraceVerificationResult> VerifyTraceCodesAsync(
        string workOrderNumber,
        List<string> traceCodes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get authentication token
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            _logger.LogInformation("Calling MES Cloud trace verification API for work order {WorkOrder}, codes count: {Count}",
                workOrderNumber, traceCodes.Count);

            // 準備請求 payload (根據 PDF 文檔)
            var requestPayload = new
            {
                woType = 1, // 固定傳 1 (根據 PDF 文檔)
                woNum = workOrderNumber,
                codes = traceCodes,
                prtMacNo = "MONITOR" // 監控端固定傳 MONITOR
            };

            // Prepare HTTP request to MES Cloud
            // URL: POST /CimforceTraceMgrDev/api/transcode/checkcode
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/transcode/checkcode")
            {
                Content = JsonContent.Create(requestPayload, options: new JsonSerializerOptions
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

                // Retry with new token - need to create a new HttpRequestMessage
                token = await _tokenService.GetAccessTokenAsync(cancellationToken);

                var retryRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/transcode/checkcode")
                {
                    Content = JsonContent.Create(requestPayload, options: new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    })
                };
                retryRequest.Headers.Add("accessToken", token);

                response = await _httpClient.SendAsync(retryRequest, cancellationToken);
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var verificationResponse = JsonSerializer.Deserialize<TraceVerificationApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (verificationResponse == null)
            {
                _logger.LogError("Failed to parse MES Cloud trace verification response");
                return new TraceVerificationResult
                {
                    Success = false,
                    Message = "無法解析 MES Cloud API 回應",
                    Code = "500"
                };
            }

            _logger.LogInformation("MES Cloud trace verification result: Success={Success}, Code={Code}, Message={Message}",
                verificationResponse.Success, verificationResponse.Code, verificationResponse.Msg);

            return new TraceVerificationResult
            {
                Success = verificationResponse.Success,
                Message = verificationResponse.Msg ?? string.Empty,
                Code = verificationResponse.Code ?? "0"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for trace verification - MES Cloud may not be reachable");
            return new TraceVerificationResult
            {
                Success = false,
                Message = "無法連線到 MES Cloud",
                Code = "0"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call MES Cloud trace verification API");
            return new TraceVerificationResult
            {
                Success = false,
                Message = $"呼叫 MES Cloud API 失敗: {ex.Message}",
                Code = "500"
            };
        }
    }

    // DTO for Monitor's /api/status response
    private class StatusApiResponse
    {
        public string ConnectionStatus { get; set; } = string.Empty;
        public DateTime LastActivity { get; set; }
        public StatisticsData? Statistics { get; set; }
        public DateTime Timestamp { get; set; }
        public string? MesCloudStatus { get; set; }
    }

    private class StatisticsData
    {
        public int TotalReceived { get; set; }
        public int SuccessfulUploads { get; set; }
        public int QueuedUploads { get; set; }
        public int CurrentQueueSize { get; set; }
    }

    // DTO for /api/status/history response
    private class HistoryApiItem
    {
        public DateTime Timestamp { get; set; }
        public string TraceCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // DTO for /api/status/upload-history response
    private class MesUploadHistoryItem
    {
        public int Id { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? MachineNumber { get; set; }
        public string? TraceCode { get; set; }
        public string? LotNo { get; set; }
        public string? PartNumber { get; set; }
        public string? ProcessName { get; set; }
        public string? DeviceName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ResponseMessage { get; set; }
        public int? ResponseCode { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    // DTO for /api/trace/verify response
    private class TraceVerificationApiResponse
    {
        public bool Success { get; set; }
        public object? Data { get; set; }
        public string? Msg { get; set; }
        public string? Code { get; set; }
    }
}
