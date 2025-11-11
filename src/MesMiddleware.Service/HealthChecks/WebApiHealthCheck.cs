using Microsoft.Extensions.Diagnostics.HealthChecks;
using MesMiddleware.Service.Services.WebApi;

namespace MesMiddleware.Service.HealthChecks;

/// <summary>
/// Health check to verify connectivity to MES WebAPI.
/// </summary>
public class WebApiHealthCheck : IHealthCheck
{
    private readonly IMesWebApiClient _webApiClient;

    public WebApiHealthCheck(IMesWebApiClient webApiClient)
    {
        _webApiClient = webApiClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = await _webApiClient.CheckConnectionAsync(cancellationToken);

            return isConnected
                ? HealthCheckResult.Healthy("WebAPI is reachable and responding")
                : HealthCheckResult.Degraded("WebAPI returned unhealthy response");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("WebAPI is unreachable", ex);
        }
    }
}
