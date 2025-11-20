using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesMiddleware.DeviceSimulator.Services;
using MesMiddleware.Shared.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace MesMiddleware.DeviceSimulator.ViewModels;

/// <summary>
/// 設備端模擬器主視圖模型
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly InspectionDataGenerator _dataGenerator;
    private readonly MiddlewareApiClient _apiClient;
    private System.Timers.Timer? _autoSendTimer;

    [ObservableProperty]
    private string _serverUrl = "http://localhost:5100";

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private int _totalSent = 0;

    [ObservableProperty]
    private int _successCount = 0;

    [ObservableProperty]
    private int _failureCount = 0;

    [ObservableProperty]
    private ObservableCollection<SubmitResultViewModel> _submitHistory = new();

    // 手動發送表單
    [ObservableProperty]
    private string _manualTraceCode = "";

    [ObservableProperty]
    private string _manualLotNo = "";

    [ObservableProperty]
    private string _manualResult = "OK";

    // 批量發送設定
    [ObservableProperty]
    private int _batchCount = 10;

    [ObservableProperty]
    private int _batchDelayMs = 100;

    // 自動發送設定
    [ObservableProperty]
    private bool _isAutoSending = false;

    [ObservableProperty]
    private int _autoSendIntervalSeconds = 5;

    public MainViewModel()
    {
        _dataGenerator = new InspectionDataGenerator();
        _apiClient = new MiddlewareApiClient();
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        try
        {
            _apiClient.SetBaseUrl(ServerUrl);
            IsConnected = await _apiClient.TestConnectionAsync();

            if (IsConnected)
            {
                MessageBox.Show($"連接成功!\n\n服務器: {ServerUrl}", "連接測試",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"連接失敗!\n\n請確認服務器 {ServerUrl} 是否運行中", "連接測試",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            MessageBox.Show($"連接測試失敗: {ex.Message}", "錯誤",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void GenerateRandomTraceCode()
    {
        ManualTraceCode = _dataGenerator.GenerateRandomTraceCode();
    }

    [RelayCommand]
    private void GenerateRandomLotNo()
    {
        ManualLotNo = _dataGenerator.GenerateRandomLotNo();
    }

    [RelayCommand]
    private void GenerateRandomResult()
    {
        ManualResult = _dataGenerator.GenerateRandomResult();
    }

    [RelayCommand]
    private async Task SendManualDataAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualTraceCode) || string.IsNullOrWhiteSpace(ManualLotNo))
        {
            MessageBox.Show("請先生成 TraceCode 和 LotNo!", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var record = _dataGenerator.GenerateSingle(ManualTraceCode, ManualLotNo, ManualResult);
            var result = await _apiClient.SubmitInspectionAsync(record);

            AddSubmitResult(result);
            UpdateStatistics();

            if (result.IsSuccess)
            {
                MessageBox.Show($"發送成功!\n\nTraceCode: {result.TraceCode}\n耗時: {result.ElapsedMs}ms",
                    "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"發送失敗!\n\n錯誤: {result.ErrorMessage}", "失敗",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"發送失敗: {ex.Message}", "錯誤",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SendBatchDataAsync()
    {
        if (BatchCount <= 0 || BatchCount > 1000)
        {
            MessageBox.Show("批量數量必須在 1-1000 之間!", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var records = _dataGenerator.GenerateBatch(BatchCount);
            var results = await _apiClient.SubmitBatchAsync(records, BatchDelayMs);

            foreach (var result in results)
            {
                AddSubmitResult(result);
            }

            UpdateStatistics();

            var successCount = results.Count(r => r.IsSuccess);
            MessageBox.Show($"批量發送完成!\n\n總數: {BatchCount}\n成功: {successCount}\n失敗: {BatchCount - successCount}",
                "批量發送", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"批量發送失敗: {ex.Message}", "錯誤",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ToggleAutoSend()
    {
        if (IsAutoSending)
        {
            // 停止自動發送
            _autoSendTimer?.Stop();
            _autoSendTimer?.Dispose();
            _autoSendTimer = null;
            IsAutoSending = false;
        }
        else
        {
            // 啟動自動發送
            if (AutoSendIntervalSeconds < 1 || AutoSendIntervalSeconds > 3600)
            {
                MessageBox.Show("自動發送間隔必須在 1-3600 秒之間!", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _autoSendTimer = new System.Timers.Timer(AutoSendIntervalSeconds * 1000);
            _autoSendTimer.Elapsed += async (s, e) => await AutoSendDataAsync();
            _autoSendTimer.Start();
            IsAutoSending = true;

            MessageBox.Show($"已啟動自動發送 (間隔: {AutoSendIntervalSeconds} 秒)", "自動發送",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void ClearHistory()
    {
        var result = MessageBox.Show("確定要清空歷史記錄嗎?", "確認",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            SubmitHistory.Clear();
            TotalSent = 0;
            SuccessCount = 0;
            FailureCount = 0;
        }
    }

    private async Task AutoSendDataAsync()
    {
        try
        {
            var record = _dataGenerator.GenerateSingle();
            var result = await _apiClient.SubmitInspectionAsync(record);

            Application.Current.Dispatcher.Invoke(() =>
            {
                AddSubmitResult(result);
                UpdateStatistics();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"自動發送失敗: {ex.Message}");
        }
    }

    private void AddSubmitResult(SubmitResult result)
    {
        var vm = new SubmitResultViewModel
        {
            TraceCode = result.TraceCode,
            SentTime = result.SentTime,
            StatusText = result.StatusText,
            ElapsedMs = result.ElapsedMs,
            ErrorMessage = result.ErrorMessage,
            IsSuccess = result.IsSuccess
        };

        // 保持最新 100 條記錄
        if (SubmitHistory.Count >= 100)
        {
            SubmitHistory.RemoveAt(SubmitHistory.Count - 1);
        }

        SubmitHistory.Insert(0, vm);
    }

    private void UpdateStatistics()
    {
        TotalSent = SubmitHistory.Count;
        SuccessCount = SubmitHistory.Count(r => r.IsSuccess);
        FailureCount = SubmitHistory.Count(r => !r.IsSuccess);
    }
}

/// <summary>
/// 提交結果視圖模型
/// </summary>
public class SubmitResultViewModel
{
    public string TraceCode { get; set; } = string.Empty;
    public DateTime SentTime { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }

    public string SentTimeText => SentTime.ToString("HH:mm:ss");
}
