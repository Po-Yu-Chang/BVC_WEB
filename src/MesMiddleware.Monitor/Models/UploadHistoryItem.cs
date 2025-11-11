namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 上傳歷史項目，用於監控儀表板顯示
/// </summary>
public class UploadHistoryItem
{
    /// <summary>
    /// 時間戳記
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 追蹤碼或批號
    /// </summary>
    public required string TraceCodeOrLotNo { get; set; }

    /// <summary>
    /// 設備名稱
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// 狀態："Success"（成功）、"Queued"（排隊中）、"Failed"（失敗）
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// 錯誤訊息（如果有）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 重試次數
    /// </summary>
    public int RetryCount { get; set; }
}
