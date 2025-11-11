namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 代表單一上傳記錄，用於歷史視圖顯示
/// 對應至服務資料庫中的 QueuedUpload 實體
/// </summary>
public class UploadRecord
{
    /// <summary>
    /// 記錄唯一識別碼
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 時間戳記
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 追蹤碼
    /// </summary>
    public string TraceCode { get; set; } = string.Empty;

    /// <summary>
    /// 設備名稱
    /// </summary>
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>
    /// 狀態：Success（成功）、Failed（失敗）、Pending（等待中）
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 錯誤訊息（如果有）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
