namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 中介軟體服務連線狀態的資料傳輸物件（DTO）
/// </summary>
public class ConnectionStatusDto
{
    /// <summary>
    /// 連線狀態：Connected（已連線）、Disconnected（已斷線）、Retrying（重試中）
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 最後 Ping 的時間戳記
    /// </summary>
    public DateTime LastPingTimestamp { get; set; }

    /// <summary>
    /// 佇列深度
    /// </summary>
    public int QueueDepth { get; set; }
}
