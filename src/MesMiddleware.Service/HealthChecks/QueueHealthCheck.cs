using Microsoft.Extensions.Diagnostics.HealthChecks;
using MesMiddleware.Service.Services.Queue;

namespace MesMiddleware.Service.HealthChecks;

/// <summary>
/// Health check to monitor upload queue depth.
/// Reports degraded if queue depth exceeds threshold.
/// </summary>
public class QueueHealthCheck : IHealthCheck
{
    private readonly IUploadQueueService _queueService;
    private readonly int _warningThreshold;
    private readonly int _criticalThreshold;

    public QueueHealthCheck(
        IUploadQueueService queueService,
        int warningThreshold = 100,
        int criticalThreshold = 500)
    {
        _queueService = queueService;
        _warningThreshold = warningThreshold;
        _criticalThreshold = criticalThreshold;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queueDepth = await _queueService.GetQueueDepthAsync(cancellationToken);

            if (queueDepth >= _criticalThreshold)
            {
                return HealthCheckResult.Unhealthy(
                    $"Queue depth critical: {queueDepth} items (threshold: {_criticalThreshold})");
            }

            if (queueDepth >= _warningThreshold)
            {
                return HealthCheckResult.Degraded(
                    $"Queue depth elevated: {queueDepth} items (threshold: {_warningThreshold})");
            }

            return HealthCheckResult.Healthy($"Queue depth normal: {queueDepth} items");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check queue depth", ex);
        }
    }
}
