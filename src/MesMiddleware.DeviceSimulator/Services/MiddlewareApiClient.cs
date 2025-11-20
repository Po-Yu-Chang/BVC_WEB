using MesMiddleware.Shared.Models;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;

namespace MesMiddleware.DeviceSimulator.Services;

/// <summary>
/// Middleware API 客戶端 - 負責與 MesMiddleware.Service 通訊
/// </summary>
public class MiddlewareApiClient
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://localhost:5100";

    public MiddlewareApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// 設定連接 URL
    /// </summary>
    public void SetBaseUrl(string url)
    {
        _baseUrl = url.TrimEnd('/');
    }

    /// <summary>
    /// 提交檢測數據
    /// </summary>
    public async Task<SubmitResult> SubmitInspectionAsync(InspectionRecord record)
    {
        var result = new SubmitResult
        {
            TraceCode = record.TraceCode,
            SentTime = DateTime.Now
        };

        try
        {
            var json = JsonConvert.SerializeObject(record);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/inspection/submit", content);

            result.ResponseTime = DateTime.Now;
            result.StatusCode = (int)response.StatusCode;
            result.IsSuccess = response.IsSuccessStatusCode;

            var responseBody = await response.Content.ReadAsStringAsync();
            result.ResponseMessage = responseBody;

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"HTTP {result.StatusCode}: {responseBody}";
            }
        }
        catch (HttpRequestException ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"網絡錯誤: {ex.Message}";
            result.ResponseTime = DateTime.Now;
        }
        catch (TaskCanceledException ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"請求超時: {ex.Message}";
            result.ResponseTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"未知錯誤: {ex.Message}";
            result.ResponseTime = DateTime.Now;
        }

        return result;
    }

    /// <summary>
    /// 批量提交檢測數據
    /// </summary>
    public async Task<List<SubmitResult>> SubmitBatchAsync(List<InspectionRecord> records, int delayMs = 100)
    {
        var results = new List<SubmitResult>();

        foreach (var record in records)
        {
            var result = await SubmitInspectionAsync(record);
            results.Add(result);

            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }
        }

        return results;
    }

    /// <summary>
    /// 測試連接
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/status");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 提交結果
/// </summary>
public class SubmitResult
{
    public string TraceCode { get; set; } = string.Empty;
    public DateTime SentTime { get; set; }
    public DateTime? ResponseTime { get; set; }
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string ResponseMessage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    public long ElapsedMs => ResponseTime.HasValue
        ? (long)(ResponseTime.Value - SentTime).TotalMilliseconds
        : 0;

    public string StatusText => IsSuccess ? "成功" : "失敗";
}
