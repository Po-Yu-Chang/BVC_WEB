namespace MesMiddleware.Service.Models;

/// <summary>
/// Configuration options for WebAPI client connection.
/// Bound from appsettings.json "WebApi" section.
/// </summary>
public class WebApiOptions
{
    public const string SectionName = "WebApi";

    /// <summary>
    /// Base URL of the MES WebAPI service (e.g., "http://localhost:5000")
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// HTTP request timeout in seconds (default 30)
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Machine number for authentication (registered in MES system)
    /// </summary>
    public string MachineNumber { get; set; } = "MACHINE001";

    /// <summary>
    /// Machine IP address for authentication (IP-based access control)
    /// </summary>
    public string MachineIp { get; set; } = "192.168.1.100";
}
