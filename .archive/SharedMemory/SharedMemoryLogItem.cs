namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 共享記憶體活動日誌項目，用於監控儀表板顯示
/// </summary>
public class SharedMemoryLogItem
{
    /// <summary>
    /// 時間戳記
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 事件類型："DataReceived"（接收資料）、"ParseError"（解析錯誤）、"ValidationError"（驗證錯誤）
    /// </summary>
    public required string EventType { get; set; }

    /// <summary>
    /// 訊息內容
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// 追蹤碼或批號（如果有）
    /// </summary>
    public string? TraceCodeOrLotNo { get; set; }

    /// <summary>
    /// 資料大小（位元組）
    /// </summary>
    public int DataSizeBytes { get; set; }
}
