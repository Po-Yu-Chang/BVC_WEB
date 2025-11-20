namespace MesMiddleware.Simulator.Models;

/// <summary>
/// 設備登錄記錄
/// </summary>
public class DeviceLogin
{
    public int Id { get; set; }

    /// <summary>
    /// 機台編號
    /// </summary>
    public string PrtMacNo { get; set; } = string.Empty;

    /// <summary>
    /// 機台註冊 IP
    /// </summary>
    public string IpAddr { get; set; } = string.Empty;

    /// <summary>
    /// Token (登錄憑證)
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 登錄時間
    /// </summary>
    public DateTime LoginTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 最後使用時間
    /// </summary>
    public DateTime LastUsedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Token 是否有效
    /// </summary>
    public bool IsActive { get; set; } = true;
}
