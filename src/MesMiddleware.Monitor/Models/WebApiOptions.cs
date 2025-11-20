namespace MesMiddleware.Monitor.Models;

/// <summary>
/// Configuration options for MES Cloud WebAPI connection.
/// Bound from appsettings.json "WebApi" section.
/// </summary>
public class WebApiOptions
{
    public const string SectionName = "WebApi";

    /// <summary>
    /// Base URL of the MES Cloud WebAPI service (e.g., "http://192.168.1.100:8080")
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// HTTP request timeout in seconds (default 30)
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Machine number for authentication (registered in MES system)
    /// 設備編號，用於向 MES Cloud 登入取得 Token
    /// </summary>
    public string MachineNumber { get; set; } = "MACHINE001";

    /// <summary>
    /// Machine IP address for authentication (IP-based access control)
    /// 設備 IP 位址，用於 Referrer header
    /// </summary>
    public string MachineIp { get; set; } = "192.168.1.100";
}
