namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 中介軟體服務連線狀態的資料傳輸物件(DTO)
/// </summary>
public class ConnectionStatusDto
{
    /// <summary>
    /// 連線狀態:Connected(已連線)、Disconnected(已斷線)、Retrying(重試中)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 最後活動的時間戳記
    /// </summary>
    public DateTime LastPingTimestamp { get; set; }

    /// <summary>
    /// 佇列深度 (pending + retrying)
    /// </summary>
    public int QueueDepth { get; set; }

    /// <summary>
    /// 總共接收的資料筆數
    /// </summary>
    public int TotalReceived { get; set; }

    /// <summary>
    /// 成功上傳的筆數
    /// </summary>
    public int SuccessfulUploads { get; set; }

    /// <summary>
    /// 排隊上傳的筆數
    /// </summary>
    public int QueuedUploads { get; set; }

    /// <summary>
    /// MES Cloud 連線狀態: Connected(已連線)、Disconnected(已斷線)
    /// </summary>
    public string MesCloudStatus { get; set; } = string.Empty;
}
