using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.IO.MemoryMappedFiles;

namespace MesMiddleware.Service.HealthChecks;

/// <summary>
/// Health check to verify shared memory segments are accessible.
/// </summary>
public class SharedMemoryHealthCheck : IHealthCheck
{
    private readonly string _segmentName;

    public SharedMemoryHealthCheck(string segmentName = "MES_INSPECTION_DATA")
    {
        _segmentName = segmentName;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to open the shared memory segment (read-only check)
            using var segment = MemoryMappedFile.OpenExisting(_segmentName, MemoryMappedFileRights.Read);

            return Task.FromResult(
                HealthCheckResult.Healthy($"Shared memory segment '{_segmentName}' is accessible"));
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded($"Shared memory segment '{_segmentName}' not found - equipment may not be running"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"Failed to access shared memory segment '{_segmentName}': {ex.Message}", ex));
        }
    }
}
