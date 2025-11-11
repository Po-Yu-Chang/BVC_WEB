namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 代表共享記憶體活動日誌項目
/// </summary>
public class SharedMemoryActivity
{
    /// <summary>
    /// 時間戳記
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 事件類型：Read（讀取）、Write（寫入）
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 資料大小
    /// </summary>
    public long DataSize { get; set; }

    /// <summary>
    /// 處理狀態
    /// </summary>
    public string ProcessingStatus { get; set; } = string.Empty;
}
