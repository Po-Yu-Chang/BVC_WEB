using MesMiddleware.DeviceSimulator.ViewModels;
using MesMiddleware.Shared.Models.LabView;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MesMiddleware.DeviceSimulator.Services;

/// <summary>
/// Middleware API 客戶端 - 負責與 MesMiddleware.Service 通訊
/// 使用 LabVIEW 格式發送資料到 /api/labview/submit
/// </summary>
public class MiddlewareApiClient
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "http://localhost:5100";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public MiddlewareApiClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
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
    /// 使用 LabVIEW 格式提交檢測數據
    /// </summary>
    public async Task<SubmitResult> SubmitLabViewDataAsync(LabViewInspectionRequest request)
    {
        var result = new SubmitResult
        {
            TraceCode = request.Data.FirstOrDefault()?.TraceCode ?? "N/A",
            SentTime = DateTime.Now
        };

        try
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            result.RequestJson = json;

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/labview/submit", content);

            result.ResponseTime = DateTime.Now;
            result.StatusCode = (int)response.StatusCode;

            var responseBody = await response.Content.ReadAsStringAsync();
            result.ResponseMessage = responseBody;

            // 解析 MES 格式的回應
            try
            {
                var mesResponse = JsonSerializer.Deserialize<MesApiResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                result.IsSuccess = mesResponse?.Success == true || response.IsSuccessStatusCode;
                if (!result.IsSuccess)
                {
                    result.ErrorMessage = mesResponse?.Msg ?? $"HTTP {result.StatusCode}";
                }
            }
            catch
            {
                result.IsSuccess = response.IsSuccessStatusCode;
                if (!result.IsSuccess)
                {
                    result.ErrorMessage = $"HTTP {result.StatusCode}: {responseBody}";
                }
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
    /// 從 ViewModel 資料建立 LabVIEW 請求
    /// </summary>
    public LabViewInspectionRequest BuildLabViewRequest(
        string procName,
        string devName,
        string userName,
        string workClass,
        string traceCode,
        string lotNo,
        string partNumber,
        string remark,
        IEnumerable<EditableParamDataItem> paramData,
        IEnumerable<EditableBenchmarkItem> benchmarks,
        IEnumerable<EditableOtherDataItem> otherData)
    {
        var request = new LabViewInspectionRequest
        {
            IsVerifyLot = false,
            Data = new List<LabViewInspectionData>
            {
                new LabViewInspectionData
                {
                    ProcName = procName,
                    DevName = devName,
                    UserName = userName,
                    WorkClass = workClass,
                    TraceCode = traceCode,
                    LotNo = lotNo,
                    PartNumber = partNumber,
                    Remark = remark,
                    ParamData = paramData.Select(p => new LabViewParamDataItem
                    {
                        Code = p.Code,
                        Name = p.Name,
                        Value = p.Value,
                        Unit = p.Unit,
                        Desc = p.Desc
                    }).ToList(),
                    Benchmarks = benchmarks.Select(b => new LabViewBenchmarkItem
                    {
                        Code = b.Code,
                        Name = b.Name,
                        Value = b.Value,
                        Unit = b.Unit,
                        Desc = b.Desc
                    }).ToList(),
                    OtherData = otherData.Select(o => new LabViewOtherDataItem
                    {
                        Code = o.Code,
                        Name = o.Name,
                        Value = o.Value,
                        Unit = o.Unit,
                        Desc = o.Desc
                    }).ToList()
                }
            }
        };

        return request;
    }

    /// <summary>
    /// 批量提交檢測數據
    /// </summary>
    public async Task<List<SubmitResult>> SubmitBatchAsync(List<LabViewInspectionRequest> requests, int delayMs = 100)
    {
        var results = new List<SubmitResult>();

        foreach (var request in requests)
        {
            var result = await SubmitLabViewDataAsync(request);
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

    /// <summary>
    /// 取得預覽 JSON 字串
    /// </summary>
    public string GetPreviewJson(LabViewInspectionRequest request)
    {
        return JsonSerializer.Serialize(request, JsonOptions);
    }

    /// <summary>
    /// 直接發送 Raw JSON 字串到 LabVIEW 端點
    /// </summary>
    public async Task<SubmitResult> SubmitRawJsonAsync(string rawJson)
    {
        var result = new SubmitResult
        {
            TraceCode = "RAW-JSON",
            SentTime = DateTime.Now,
            RequestJson = rawJson
        };

        try
        {
            // 嘗試解析 JSON 以取得 TraceCode
            try
            {
                var request = JsonSerializer.Deserialize<LabViewInspectionRequest>(rawJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                result.TraceCode = request?.Data?.FirstOrDefault()?.TraceCode ?? "RAW-JSON";
            }
            catch
            {
                // 解析失敗，使用預設值
            }

            var content = new StringContent(rawJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/labview/submit", content);

            result.ResponseTime = DateTime.Now;
            result.StatusCode = (int)response.StatusCode;

            var responseBody = await response.Content.ReadAsStringAsync();
            result.ResponseMessage = responseBody;

            // 解析 MES 格式的回應
            try
            {
                var mesResponse = JsonSerializer.Deserialize<MesApiResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                result.IsSuccess = mesResponse?.Success == true || response.IsSuccessStatusCode;
                if (!result.IsSuccess)
                {
                    result.ErrorMessage = mesResponse?.Msg ?? $"HTTP {result.StatusCode}";
                }
            }
            catch
            {
                result.IsSuccess = response.IsSuccessStatusCode;
                if (!result.IsSuccess)
                {
                    result.ErrorMessage = $"HTTP {result.StatusCode}: {responseBody}";
                }
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
}

/// <summary>
/// MES API 回應格式
/// </summary>
public class MesApiResponse
{
    public bool Success { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Msg { get; set; } = string.Empty;
    public object? Data { get; set; }
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
    public string RequestJson { get; set; } = string.Empty;

    public long ElapsedMs => ResponseTime.HasValue
        ? (long)(ResponseTime.Value - SentTime).TotalMilliseconds
        : 0;

    public string StatusText => IsSuccess ? "成功" : "失敗";
}
