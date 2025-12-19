namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 上傳歷史記錄 - 記錄所有上傳嘗試的歷史
/// </summary>
public class UploadHistory
{
    /// <summary>
    /// 唯一識別碼
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 追溯碼
    /// </summary>
    public string? TraceCode { get; set; }

    /// <summary>
    /// 批號
    /// </summary>
    public string? LotNo { get; set; }

    /// <summary>
    /// 上傳時間
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// 狀態 (Success, Failed)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 重試次數
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 資料來源 (Device, Queue)
    /// </summary>
    public string Source { get; set; } = "Device";

    /// <summary>
    /// 關聯的佇列項目 ID (如果是從佇列重試的)
    /// </summary>
    public int? QueueItemId { get; set; }
}
