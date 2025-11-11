using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// View model for service status display.
/// Shows connection state, statistics, and uptime.
/// </summary>
public partial class StatusViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<StatusViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private string _statusColor = "Red";

    [ObservableProperty]
    private DateTime _lastPingTime;

    [ObservableProperty]
    private int _queueDepth;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    // Legacy properties for compatibility
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isWebApiConnected;

    [ObservableProperty]
    private bool _isSharedMemoryActive;

    [ObservableProperty]
    private int _totalDataReceived;

    [ObservableProperty]
    private int _successfulUploads;

    [ObservableProperty]
    private int _queuedUploads;

    [ObservableProperty]
    private string _lastError = string.Empty;

    [ObservableProperty]
    private DateTime _lastUpdated;

    [ObservableProperty]
    private string _uptime = "00:00:00";

    public StatusViewModel(IMiddlewareApiClient apiClient, ILogger<StatusViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task RefreshStatusAsync()
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        try
        {
            var status = await _apiClient.GetConnectionStatusAsync();

            ConnectionStatus = status.Status;
            LastPingTime = status.LastPingTimestamp;
            QueueDepth = status.QueueDepth;

            // Set status color based on connection state
            StatusColor = status.Status switch
            {
                "Connected" => "Green",
                "Disconnected" => "Red",
                "Retrying" => "Yellow",
                _ => "Gray"
            };

            ErrorMessage = string.Empty;
            _logger.LogDebug("Status refreshed: {Status}", ConnectionStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh status");
            ConnectionStatus = "Error";
            StatusColor = "Red";
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshStatusAsync();
    }

    public void StartAutoRefresh(TimeSpan? interval = null)
    {
        _refreshCts = new CancellationTokenSource();
        var refreshInterval = interval ?? TimeSpan.FromSeconds(2);
        _refreshTask = PeriodicRefreshLoopAsync(refreshInterval, _refreshCts.Token);
    }

    public void StopAutoRefresh()
    {
        _refreshCts?.Cancel();
        // Don't wait for task completion - immediate shutdown
        // _refreshTask?.Wait(TimeSpan.FromSeconds(5));
        _refreshCts?.Dispose();
    }

    public async Task StartPeriodicRefreshAsync()
    {
        StartAutoRefresh();
        await Task.CompletedTask;
    }

    public void StopPeriodicRefresh()
    {
        StopAutoRefresh();
    }

    private async Task PeriodicRefreshLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshStatusAsync();
                await Task.Delay(interval, cancellationToken);
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
