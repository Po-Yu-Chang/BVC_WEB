using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// View model for upload queue status display.
/// Shows pending, retrying, and failed upload counts.
/// </summary>
public partial class QueueViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<QueueViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _retryingCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private int _totalDepth;

    [ObservableProperty]
    private string _oldestQueuedAt = "N/A";

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _lastError = string.Empty;

    public QueueViewModel(IMiddlewareApiClient apiClient, ILogger<QueueViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        try
        {
            // Use connection status to get queue depth
            var connectionStatus = await _apiClient.GetConnectionStatusAsync();

            // Get upload history to calculate status breakdown
            var history = await _apiClient.GetUploadHistoryAsync(maxRecords: 1000);

            PendingCount = history.Count(h => h.Status == "Pending");
            RetryingCount = history.Count(h => h.Status == "Retrying");
            FailedCount = history.Count(h => h.Status == "Failed");
            TotalDepth = connectionStatus.QueueDepth;

            var oldestPending = history
                .Where(h => h.Status == "Pending" || h.Status == "Retrying")
                .OrderBy(h => h.Timestamp)
                .FirstOrDefault();

            OldestQueuedAt = oldestPending?.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

            LastError = string.Empty;
            _logger.LogDebug("Queue status refreshed: Pending={Pending}, Retrying={Retrying}, Failed={Failed}",
                PendingCount, RetryingCount, FailedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh queue status");
            LastError = $"Failed to refresh: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task StartPeriodicRefreshAsync()
    {
        _refreshCts = new CancellationTokenSource();
        _refreshTask = PeriodicRefreshLoopAsync(_refreshCts.Token);
        await Task.CompletedTask;
    }

    public void StopPeriodicRefresh()
    {
        _refreshCts?.Cancel();
        // Don't wait for task completion - immediate shutdown
        // _refreshTask?.Wait(TimeSpan.FromSeconds(5));
        _refreshCts?.Dispose();
    }

    private async Task PeriodicRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync();
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in periodic refresh loop");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}
