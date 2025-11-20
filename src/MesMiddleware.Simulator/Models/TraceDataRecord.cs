namespace MesMiddleware.Simulator.Models;

/// <summary>
/// 追溯數據記錄
/// </summary>
public class TraceDataRecord
{
    public int Id { get; set; }

    /// <summary>
    /// 上傳時間
    /// </summary>
    public DateTime UploadTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 追溯二維碼
    /// </summary>
    public string? TraceCode { get; set; }

    /// <summary>
    /// LOT 號
    /// </summary>
    public string? LotNo { get; set; }

    /// <summary>
    /// 站點(工步)編碼
    /// </summary>
    public string ProcName { get; set; } = string.Empty;

    /// <summary>
    /// 設備編號
    /// </summary>
    public string DevName { get; set; } = string.Empty;

    /// <summary>
    /// 作業員編號
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 產品型號
    /// </summary>
    public string? PartNumber { get; set; }

    /// <summary>
    /// 完整 JSON 數據
    /// </summary>
    public string FullJsonData { get; set; } = string.Empty;

    /// <summary>
    /// 是否驗證 LOT
    /// </summary>
    public bool IsVerifyLot { get; set; }
}
