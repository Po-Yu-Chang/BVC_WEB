namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 服務狀態資訊，用於監控儀表板顯示
/// </summary>
public class ServiceStatus
{
    /// <summary>
    /// 服務是否正在運行
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// MES Cloud API 是否已連線
    /// </summary>
    public bool IsWebApiConnected { get; set; }

    /// <summary>
    /// 總共接收的資料筆數
    /// </summary>
    public int TotalDataReceived { get; set; }

    /// <summary>
    /// 成功上傳的筆數
    /// </summary>
    public int SuccessfulUploads { get; set; }

    /// <summary>
    /// 排隊中的上傳筆數
    /// </summary>
    public int QueuedUploads { get; set; }

    /// <summary>
    /// 最後更新時間
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;

    /// <summary>
    /// 最後發生的錯誤訊息
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 服務運行時間
    /// </summary>
    public TimeSpan Uptime { get; set; }
}
