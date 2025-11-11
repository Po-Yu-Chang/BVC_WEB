using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// 上傳佇列狀態顯示的視圖模型
/// 顯示等待中、重試中和失敗的上傳數量
/// </summary>
public partial class QueueViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<QueueViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    /// <summary>
    /// 等待中的項目數量
    /// </summary>
    [ObservableProperty]
    private int _pendingCount;

    /// <summary>
    /// 重試中的項目數量
    /// </summary>
    [ObservableProperty]
    private int _retryingCount;

    /// <summary>
    /// 失敗的項目數量
    /// </summary>
    [ObservableProperty]
    private int _failedCount;

    /// <summary>
    /// 佇列總深度
    /// </summary>
    [ObservableProperty]
    private int _totalDepth;

    /// <summary>
    /// 最舊的排隊時間（顯示字串）
    /// </summary>
    [ObservableProperty]
    private string _oldestQueuedAt = "N/A";

    /// <summary>
    /// 是否正在刷新
    /// </summary>
    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>
    /// 最後的錯誤訊息
    /// </summary>
    [ObservableProperty]
    private string _lastError = string.Empty;

    /// <summary>
    /// 建構函式，初始化佇列視圖模型
    /// </summary>
    public QueueViewModel(IMiddlewareApiClient apiClient, ILogger<QueueViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// 刷新命令，更新佇列狀態資訊
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        // 如果正在刷新，則跳過
        if (IsRefreshing) return;

        IsRefreshing = true;
        try
        {
            // 使用連線狀態取得佇列深度
            var connectionStatus = await _apiClient.GetConnectionStatusAsync();

            // 取得上傳歷史以計算狀態分佈
            var history = await _apiClient.GetUploadHistoryAsync(maxRecords: 1000);

            // 統計各狀態的數量
            PendingCount = history.Count(h => h.Status == "Pending");
            RetryingCount = history.Count(h => h.Status == "Retrying");
            FailedCount = history.Count(h => h.Status == "Failed");
            TotalDepth = connectionStatus.QueueDepth;

            // 找出最舊的等待或重試項目
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

    /// <summary>
    /// 啟動定期刷新
    /// </summary>
    public async Task StartPeriodicRefreshAsync()
    {
        _refreshCts = new CancellationTokenSource();
        _refreshTask = PeriodicRefreshLoopAsync(_refreshCts.Token);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 停止定期刷新
    /// </summary>
    public void StopPeriodicRefresh()
    {
        _refreshCts?.Cancel();
        // 不等待任務完成 - 立即關閉
        // _refreshTask?.Wait(TimeSpan.FromSeconds(5));
        _refreshCts?.Dispose();
    }

    /// <summary>
    /// 定期刷新迴圈
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task PeriodicRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync();
                // 每 3 秒刷新一次
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 取消操作，退出迴圈
                break;
            }
            catch (Exception ex)
            {
                // 發生錯誤時記錄並等待 5 秒後重試
                _logger.LogError(ex, "Error in periodic refresh loop");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}
