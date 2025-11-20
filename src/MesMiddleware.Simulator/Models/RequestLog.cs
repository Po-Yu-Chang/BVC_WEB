namespace MesMiddleware.Simulator.Models;

/// <summary>
/// HTTP 請求日誌 (保存完整通訊資料)
/// </summary>
public class RequestLog
{
    public int Id { get; set; }

    /// <summary>
    /// 請求時間
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// HTTP 方法 (POST)
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// API 端點路徑
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 客戶端 IP 地址
    /// </summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>
    /// 請求 Headers (JSON 格式)
    /// </summary>
    public string RequestHeaders { get; set; } = string.Empty;

    /// <summary>
    /// 請求 Body (JSON 格式)
    /// </summary>
    public string RequestBody { get; set; } = string.Empty;

    /// <summary>
    /// 響應 HTTP 狀態碼
    /// </summary>
    public int ResponseStatusCode { get; set; }

    /// <summary>
    /// 響應 Body (JSON 格式)
    /// </summary>
    public string ResponseBody { get; set; } = string.Empty;

    /// <summary>
    /// 處理耗時 (毫秒)
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 錯誤訊息 (如有)
    /// </summary>
    public string? ErrorMessage { get; set; }
}
