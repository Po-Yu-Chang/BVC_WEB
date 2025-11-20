namespace MesMiddleware.Service.Models;

/// <summary>
/// 上傳歷史記錄 (Middleware → MES Cloud)
/// 記錄所有上傳到 MES Cloud 的數據（成功和失敗）
/// </summary>
public class UploadHistory
{
    /// <summary>
    /// 主鍵 (自增 ID)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 上傳時間 (UTC)
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 機台編號
    /// </summary>
    public string? MachineNumber { get; set; }

    /// <summary>
    /// 追溯碼 (TraceCode)
    /// </summary>
    public string? TraceCode { get; set; }

    /// <summary>
    /// LOT 號
    /// </summary>
    public string? LotNo { get; set; }

    /// <summary>
    /// 產品型號
    /// </summary>
    public string? PartNumber { get; set; }

    /// <summary>
    /// 站點名稱
    /// </summary>
    public string? ProcessName { get; set; }

    /// <summary>
    /// 設備名稱
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// 上傳狀態 (Success, Failed)
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// MES Cloud 回應訊息
    /// </summary>
    public string? ResponseMessage { get; set; }

    /// <summary>
    /// MES Cloud 回應代碼 (200 = 成功)
    /// </summary>
    public int? ResponseCode { get; set; }

    /// <summary>
    /// 錯誤訊息 (如果失敗)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 重試次數 (從 QueuedUpload 來的記錄)
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 原始檢測數據 JSON (可選，用於審計)
    /// </summary>
    public string? InspectionDataJson { get; set; }

    /// <summary>
    /// 資料來源 (Equipment = 直接從設備, Queue = 從離線隊列重試)
    /// </summary>
    public string Source { get; set; } = "Equipment";
}
