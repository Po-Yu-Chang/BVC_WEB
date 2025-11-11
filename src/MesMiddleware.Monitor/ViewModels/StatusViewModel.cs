using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// 服務狀態顯示的視圖模型
/// 顯示連線狀態、統計資訊和運行時間
/// </summary>
public partial class StatusViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<StatusViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    /// <summary>
    /// 連線狀態
    /// </summary>
    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    /// <summary>
    /// 狀態顏色（用於 UI 顯示）
    /// </summary>
    [ObservableProperty]
    private string _statusColor = "Red";

    /// <summary>
    /// 最後 Ping 時間
    /// </summary>
    [ObservableProperty]
    private DateTime _lastPingTime;

    /// <summary>
    /// 佇列深度
    /// </summary>
    [ObservableProperty]
    private int _queueDepth;

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 是否正在刷新
    /// </summary>
    [ObservableProperty]
    private bool _isRefreshing;

    // 舊版屬性（保留以維持相容性）
    /// <summary>
    /// 服務是否正在運行
    /// </summary>
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>
    /// Web API 是否已連線
    /// </summary>
    [ObservableProperty]
    private bool _isWebApiConnected;

    /// <summary>
    /// 共享記憶體是否活躍
    /// </summary>
    [ObservableProperty]
    private bool _isSharedMemoryActive;

    /// <summary>
    /// 總共接收的資料筆數
    /// </summary>
    [ObservableProperty]
    private int _totalDataReceived;

    /// <summary>
    /// 成功上傳的筆數
    /// </summary>
    [ObservableProperty]
    private int _successfulUploads;

    /// <summary>
    /// 排隊中的上傳筆數
    /// </summary>
    [ObservableProperty]
    private int _queuedUploads;

    /// <summary>
    /// 最後的錯誤訊息
    /// </summary>
    [ObservableProperty]
    private string _lastError = string.Empty;

    /// <summary>
    /// 最後更新時間
    /// </summary>
    [ObservableProperty]
    private DateTime _lastUpdated;

    /// <summary>
    /// 運行時間
    /// </summary>
    [ObservableProperty]
    private string _uptime = "00:00:00";

    /// <summary>
    /// 建構函式，初始化狀態視圖模型
    /// </summary>
    public StatusViewModel(IMiddlewareApiClient apiClient, ILogger<StatusViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// 刷新服務狀態資訊
    /// </summary>
    public async Task RefreshStatusAsync()
    {
        // 如果正在刷新，則跳過
        if (IsRefreshing) return;

        IsRefreshing = true;
        try
        {
            // 從 API 客戶端取得連線狀態
            var status = await _apiClient.GetConnectionStatusAsync();

            ConnectionStatus = status.Status;
            LastPingTime = status.LastPingTimestamp;
            QueueDepth = status.QueueDepth;

            // 根據連線狀態設定狀態顏色
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

    /// <summary>
    /// 刷新命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await RefreshStatusAsync();
    }

    /// <summary>
    /// 啟動自動刷新
    /// </summary>
    /// <param name="interval">刷新間隔時間，預設為 2 秒</param>
    public void StartAutoRefresh(TimeSpan? interval = null)
    {
        _refreshCts = new CancellationTokenSource();
        var refreshInterval = interval ?? TimeSpan.FromSeconds(2);
        _refreshTask = PeriodicRefreshLoopAsync(refreshInterval, _refreshCts.Token);
    }

    /// <summary>
    /// 停止自動刷新
    /// </summary>
    public void StopAutoRefresh()
    {
        _refreshCts?.Cancel();
        // 不等待任務完成 - 立即關閉
        // _refreshTask?.Wait(TimeSpan.FromSeconds(5));
        _refreshCts?.Dispose();
    }

    /// <summary>
    /// 啟動定期刷新（非同步）
    /// </summary>
    public async Task StartPeriodicRefreshAsync()
    {
        StartAutoRefresh();
        await Task.CompletedTask;
    }

    /// <summary>
    /// 停止定期刷新
    /// </summary>
    public void StopPeriodicRefresh()
    {
        StopAutoRefresh();
    }

    /// <summary>
    /// 定期刷新迴圈
    /// </summary>
    /// <param name="interval">刷新間隔時間</param>
    /// <param name="cancellationToken">取消令牌</param>
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
