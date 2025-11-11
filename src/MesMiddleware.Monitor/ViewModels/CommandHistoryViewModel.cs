using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// 命令歷史顯示的視圖模型（任務 T066）
/// 顯示設備命令及其狀態、時間戳記和確認訊息
/// 屬於使用者故事 3（雙向命令與控制）的一部分
/// </summary>
public partial class CommandHistoryViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<CommandHistoryViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    /// <summary>
    /// 命令記錄集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CommandRecord> _commandRecords = new();

    /// <summary>
    /// 篩選後的記錄集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<CommandRecord> _filteredRecords = new();

    /// <summary>
    /// 篩選文字
    /// </summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// 狀態篩選條件
    /// </summary>
    [ObservableProperty]
    private string _statusFilter = "All";

    /// <summary>
    /// 是否正在載入
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 建構函式，初始化命令歷史視圖模型
    /// </summary>
    public CommandHistoryViewModel(IMiddlewareApiClient apiClient, ILogger<CommandHistoryViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// 載入命令歷史記錄
    /// </summary>
    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        // 如果正在載入，則跳過
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            _logger.LogDebug("Loading command history from middleware API");

            // 從中介軟體取得命令歷史（最多 1000 筆）
            var commands = await _apiClient.GetCommandHistoryAsync(maxRecords: 1000);

            // 清空並更新命令記錄集合
            CommandRecords.Clear();
            foreach (var cmd in commands)
            {
                CommandRecords.Add(cmd);
            }

            _logger.LogInformation("Loaded {Count} command records", CommandRecords.Count);

            // 套用目前的篩選條件
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load command history");
            ErrorMessage = $"Failed to load command history: {ex.Message}";
            CommandRecords.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 套用篩選條件
    /// </summary>
    [RelayCommand]
    private void ApplyFilter()
    {
        FilteredRecords.Clear();

        var filtered = CommandRecords.AsEnumerable();

        // 根據文字篩選（命令類型或設備名稱）
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(r =>
                r.CommandType.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                r.EquipmentName.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        // 根據狀態篩選
        if (StatusFilter != "All")
        {
            filtered = filtered.Where(r => r.Status == StatusFilter);
        }

        // 將篩選結果加入到篩選後的記錄集合
        foreach (var record in filtered)
        {
            FilteredRecords.Add(record);
        }

        _logger.LogDebug("Filtered {Count} records from {Total} total",
            FilteredRecords.Count, CommandRecords.Count);
    }

    /// <summary>
    /// 清除篩選條件
    /// </summary>
    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
        StatusFilter = "All";
        ApplyFilter();
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
        _refreshTask?.Wait(TimeSpan.FromSeconds(5));
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
                await LoadHistoryAsync();
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

    /// <summary>
    /// 當篩選文字改變時，自動套用篩選
    /// </summary>
    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// 當狀態篩選改變時，自動套用篩選
    /// </summary>
    partial void OnStatusFilterChanged(string value)
    {
        ApplyFilter();
    }
}
