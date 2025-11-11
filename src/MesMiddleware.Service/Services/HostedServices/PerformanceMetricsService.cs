using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MesMiddleware.Service.Services.Queue;

namespace MesMiddleware.Service.Services.HostedServices;

/// <summary>
/// Background service that logs performance metrics periodically.
/// Tracks middleware latency, queue depth, and success rate.
/// </summary>
public class PerformanceMetricsService : BackgroundService
{
    private readonly IUploadQueueService _queueService;
    private readonly ILogger<PerformanceMetricsService> _logger;
    private readonly TimeSpan _logInterval = TimeSpan.FromSeconds(60);

    private int _totalProcessed = 0;
    private int _successCount = 0;
    private int _failureCount = 0;
    private readonly List<long> _latencies = new();
    private readonly object _lock = new();

    public PerformanceMetricsService(
        IUploadQueueService queueService,
        ILogger<PerformanceMetricsService> logger)
    {
        _queueService = queueService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Performance Metrics Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_logInterval, stoppingToken);
                await LogMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging performance metrics");
            }
        }

        _logger.LogInformation("Performance Metrics Service stopped");
    }

    private async Task LogMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var queueDepth = await _queueService.GetQueueDepthAsync(cancellationToken);

            lock (_lock)
            {
                var successRate = _totalProcessed > 0
                    ? (_successCount / (double)_totalProcessed) * 100
                    : 0;

                var avgLatency = _latencies.Count > 0
                    ? _latencies.Average()
                    : 0;

                var p95Latency = _latencies.Count > 0
                    ? GetPercentile(_latencies, 0.95)
                    : 0;

                var p99Latency = _latencies.Count > 0
                    ? GetPercentile(_latencies, 0.99)
                    : 0;

                _logger.LogInformation(
                    "Performance Metrics: TotalProcessed={TotalProcessed}, SuccessCount={SuccessCount}, " +
                    "FailureCount={FailureCount}, SuccessRate={SuccessRate:F2}%, QueueDepth={QueueDepth}, " +
                    "AvgLatencyMs={AvgLatency:F0}, P95LatencyMs={P95Latency:F0}, P99LatencyMs={P99Latency:F0}",
                    _totalProcessed,
                    _successCount,
                    _failureCount,
                    successRate,
                    queueDepth,
                    avgLatency,
                    p95Latency,
                    p99Latency);

                // Reset counters after logging (rolling window)
                _latencies.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect performance metrics");
        }
    }

    public void RecordUpload(bool success, long latencyMs)
    {
        lock (_lock)
        {
            _totalProcessed++;
            if (success)
                _successCount++;
            else
                _failureCount++;

            _latencies.Add(latencyMs);

            // Keep only last 1000 latencies to prevent memory growth
            if (_latencies.Count > 1000)
                _latencies.RemoveAt(0);
        }
    }

    private static double GetPercentile(List<long> values, double percentile)
    {
        if (values.Count == 0) return 0;

        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}
