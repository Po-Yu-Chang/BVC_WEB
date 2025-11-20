namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 上傳佇列項目 - 儲存失敗的上傳，等待重試
/// </summary>
public class UploadQueueItem
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
    /// 站別編號
    /// </summary>
    public string RowNo { get; set; } = string.Empty;

    /// <summary>
    /// 製程名稱
    /// </summary>
    public string? ProcName { get; set; }

    /// <summary>
    /// 設備名稱
    /// </summary>
    public string? DevName { get; set; }

    /// <summary>
    /// 作業員
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// 班別
    /// </summary>
    public string? WorkClass { get; set; }

    /// <summary>
    /// 檢驗時間
    /// </summary>
    public DateTime InspectionTime { get; set; }

    /// <summary>
    /// 檢驗參數 JSON
    /// </summary>
    public string? ParamDataJson { get; set; }

    /// <summary>
    /// 基準值 JSON
    /// </summary>
    public string? BenchmarksJson { get; set; }

    /// <summary>
    /// 其他資料 JSON
    /// </summary>
    public string? OtherDataJson { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 重試次數
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 最後重試時間
    /// </summary>
    public DateTime? LastRetryAt { get; set; }

    /// <summary>
    /// 下次重試時間
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// 最後錯誤訊息
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 狀態 (Pending, Processing, Failed, MaxRetriesExceeded)
    /// </summary>
    public string Status { get; set; } = "Pending";
}
