using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;
using System.Collections.ObjectModel;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// 上傳歷史顯示的視圖模型
/// 顯示最近的成功和失敗上傳記錄
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<HistoryViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    /// <summary>
    /// 上傳記錄集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<UploadRecord> _uploadRecords = new();

    /// <summary>
    /// 篩選後的記錄集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<UploadRecord> _filteredRecords = new();

    /// <summary>
    /// 篩選文字
    /// </summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// 狀態篩選條件
    /// </summary>
    [ObservableProperty]
    private string _statusFilter = string.Empty;

    /// <summary>
    /// 是否正在刷新
    /// </summary>
    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // 舊版屬性（保留以維持相容性）
    /// <summary>
    /// 歷史項目集合（舊版）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<UploadHistoryItem> _historyItems = new();

    /// <summary>
    /// 最後的錯誤訊息（舊版）
    /// </summary>
    [ObservableProperty]
    private string _lastError = string.Empty;

    /// <summary>
    /// 建構函式，初始化歷史視圖模型
    /// </summary>
    public HistoryViewModel(IMiddlewareApiClient apiClient, ILogger<HistoryViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// 載入上傳歷史記錄
    /// </summary>
    public async Task LoadHistoryAsync()
    {
        // 如果正在刷新，則跳過
        if (IsRefreshing) return;

        IsRefreshing = true;
        try
        {
            // 從 API 客戶端取得上傳歷史（最多 1000 筆）
            var history = await _apiClient.GetUploadHistoryAsync(maxRecords: 1000);

            // 清空並更新上傳記錄集合
            UploadRecords.Clear();
            foreach (var item in history)
            {
                UploadRecords.Add(item);
            }

            // 清空並更新篩選後的記錄集合
            FilteredRecords.Clear();
            foreach (var item in history)
            {
                FilteredRecords.Add(item);
            }

            ErrorMessage = string.Empty;
            _logger.LogDebug("History loaded: {Count} items", UploadRecords.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// 套用篩選條件
    /// </summary>
    public void ApplyFilter()
    {
        FilteredRecords.Clear();

        var filtered = UploadRecords.AsEnumerable();

        // 根據追蹤碼篩選
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(r => r.TraceCode.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        // 根據狀態篩選
        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            filtered = filtered.Where(r => r.Status.Equals(StatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        // 將篩選結果加入到篩選後的記錄集合
        foreach (var item in filtered)
        {
            FilteredRecords.Add(item);
        }
    }

    /// <summary>
    /// 清除篩選條件
    /// </summary>
    public void ClearFilter()
    {
        FilterText = string.Empty;
        StatusFilter = string.Empty;

        // 重新顯示所有記錄
        FilteredRecords.Clear();
        foreach (var item in UploadRecords)
        {
            FilteredRecords.Add(item);
        }
    }

    /// <summary>
    /// 刷新命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadHistoryAsync();
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
                // 每 5 秒刷新一次
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 取消操作，退出迴圈
                break;
            }
            catch (Exception ex)
            {
                // 發生錯誤時記錄並等待 10 秒後重試
                _logger.LogError(ex, "Error in periodic refresh loop");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}
