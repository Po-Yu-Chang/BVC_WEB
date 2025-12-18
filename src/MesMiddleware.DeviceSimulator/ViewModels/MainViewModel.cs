using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MesMiddleware.DeviceSimulator.Services;
using MesMiddleware.Shared.Models.LabView;
using System.Collections.ObjectModel;
using System.Windows;

namespace MesMiddleware.DeviceSimulator.ViewModels;

/// <summary>
/// 設備端模擬器主視圖模型 - 使用 LabVIEW 格式發送資料
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly MiddlewareApiClient _apiClient;
    private System.Timers.Timer? _autoSendTimer;
    private int _sequenceCounter = 1;
    private readonly Random _random = new();

    #region 連接狀態

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

    #endregion

    #region LabVIEW 基本欄位

    [ObservableProperty]
    private string _procName = "W3-ET";

    [ObservableProperty]
    private string _devName = "W3-WTYKJ-001";

    [ObservableProperty]
    private string _userName = "53900";

    [ObservableProperty]
    private string _workClass = "A";

    [ObservableProperty]
    private string _traceCode = "";

    [ObservableProperty]
    private string _lotNo = "";

    [ObservableProperty]
    private string _partNumber = "3FIA98338D01";

    [ObservableProperty]
    private string _remark = "";

    #endregion

    #region DataGrid 資料集合

    [ObservableProperty]
    private ObservableCollection<EditableParamDataItem> _paramDataItems;

    [ObservableProperty]
    private ObservableCollection<EditableBenchmarkItem> _benchmarkItems;

    [ObservableProperty]
    private ObservableCollection<EditableOtherDataItem> _otherDataItems;

    #endregion

    #region 發送歷史與控制

    [ObservableProperty]
    private ObservableCollection<SubmitResultViewModel> _submitHistory = new();

    [ObservableProperty]
    private string _previewJson = "";

    [ObservableProperty]
    private string _lastResponseJson = "";

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

    // 選擇的歷史項目
    [ObservableProperty]
    private SubmitResultViewModel? _selectedHistoryItem;

    #endregion

    public MainViewModel()
    {
        _apiClient = new MiddlewareApiClient();

        // 初始化預設資料
        _paramDataItems = LabViewDataTemplates.GetDefaultParamData();
        _benchmarkItems = LabViewDataTemplates.GetDefaultBenchmarks();
        _otherDataItems = LabViewDataTemplates.GetDefaultOtherData();

        // 生成初始 TraceCode 和 LotNo
        GenerateRandomTraceCode();
        GenerateRandomLotNo();

        // 更新預覽
        UpdatePreviewJson();
    }

    #region 連接測試

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        try
        {
            _apiClient.SetBaseUrl(ServerUrl);
            IsConnected = await _apiClient.TestConnectionAsync();

            if (IsConnected)
            {
                MessageBox.Show($"連接成功!\n\n服務器: {ServerUrl}\nAPI: /api/labview/submit", "連接測試",
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

    #endregion

    #region 資料生成

    [RelayCommand]
    private void GenerateRandomTraceCode()
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        TraceCode = $"O{_random.Next(1000000, 9999999)}T{date}{_sequenceCounter:D2}";
        _sequenceCounter++;
        UpdatePreviewJson();
    }

    [RelayCommand]
    private void GenerateRandomLotNo()
    {
        var batch = _random.Next(1000, 9999);
        LotNo = $"{batch:D5}156-00{_random.Next(100, 999)}-N";
        UpdatePreviewJson();
    }

    [RelayCommand]
    private void UpdateCheckTime()
    {
        var checkTimeItem = OtherDataItems.FirstOrDefault(x => x.Code == "CheckTime");
        if (checkTimeItem != null)
        {
            checkTimeItem.Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        UpdatePreviewJson();
    }

    [RelayCommand]
    private void RandomizeResult()
    {
        // 隨機設定 Result (80% PASS, 10% OPEN, 10% LEAK)
        var resultItem = ParamDataItems.FirstOrDefault(x => x.Code == "Result");
        if (resultItem != null)
        {
            var rand = _random.Next(100);
            resultItem.Value = rand < 80 ? "PASS" : (rand < 90 ? "OPEN" : "LEAK");
        }

        // 更新 CheckQty, DefectQty, OkQty
        var checkQty = _random.Next(1, 10);
        var defectQty = resultItem?.Value != "PASS" ? _random.Next(1, checkQty + 1) : 0;
        var okQty = checkQty - defectQty;

        SetParamValue("CheckQty", checkQty.ToString());
        SetParamValue("DefectQty", defectQty.ToString());
        SetParamValue("OkQty", okQty.ToString());

        UpdatePreviewJson();
    }

    private void SetParamValue(string code, string value)
    {
        var item = ParamDataItems.FirstOrDefault(x => x.Code == code);
        if (item != null)
        {
            item.Value = value;
        }
    }

    #endregion

    #region 資料集合操作

    [RelayCommand]
    private void AddParamDataItem()
    {
        ParamDataItems.Add(new EditableParamDataItem("NewCode", "NewName", "0", "", "Description"));
        UpdatePreviewJson();
    }

    [RelayCommand]
    private void RemoveParamDataItem(EditableParamDataItem? item)
    {
        if (item != null)
        {
            ParamDataItems.Remove(item);
            UpdatePreviewJson();
        }
    }

    [RelayCommand]
    private void AddBenchmarkItem()
    {
        BenchmarkItems.Add(new EditableBenchmarkItem("NewCode", "NewName", "0", "um", "Description"));
        UpdatePreviewJson();
    }

    [RelayCommand]
    private void RemoveBenchmarkItem(EditableBenchmarkItem? item)
    {
        if (item != null)
        {
            BenchmarkItems.Remove(item);
            UpdatePreviewJson();
        }
    }

    [RelayCommand]
    private void AddOtherDataItem()
    {
        OtherDataItems.Add(new EditableOtherDataItem("NewCode", "NewName", "", "", "Description"));
        UpdatePreviewJson();
    }

    [RelayCommand]
    private void RemoveOtherDataItem(EditableOtherDataItem? item)
    {
        if (item != null)
        {
            OtherDataItems.Remove(item);
            UpdatePreviewJson();
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var result = MessageBox.Show("確定要重置所有資料為預設值嗎？", "確認",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ParamDataItems = LabViewDataTemplates.GetDefaultParamData();
            BenchmarkItems = LabViewDataTemplates.GetDefaultBenchmarks();
            OtherDataItems = LabViewDataTemplates.GetDefaultOtherData();

            ProcName = "W3-ET";
            DevName = "W3-WTYKJ-001";
            UserName = "53900";
            WorkClass = "A";
            PartNumber = "3FIA98338D01";
            Remark = "";

            GenerateRandomTraceCode();
            GenerateRandomLotNo();
            UpdatePreviewJson();
        }
    }

    #endregion

    #region 發送資料

    [RelayCommand]
    private async Task SendDataAsync()
    {
        if (string.IsNullOrWhiteSpace(TraceCode) && string.IsNullOrWhiteSpace(LotNo))
        {
            MessageBox.Show("請先生成 TraceCode 或 LotNo!", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // 更新 CheckTime
            UpdateCheckTime();

            var request = BuildCurrentRequest();
            _apiClient.SetBaseUrl(ServerUrl);
            var result = await _apiClient.SubmitLabViewDataAsync(request);

            AddSubmitResult(result);
            UpdateStatistics();
            LastResponseJson = result.ResponseMessage;

            if (result.IsSuccess)
            {
                // 自動生成新的 TraceCode
                GenerateRandomTraceCode();

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
            _apiClient.SetBaseUrl(ServerUrl);
            var requests = new List<LabViewInspectionRequest>();

            for (int i = 0; i < BatchCount; i++)
            {
                GenerateRandomTraceCode();
                RandomizeResult();
                UpdateCheckTime();
                requests.Add(BuildCurrentRequest());
            }

            var results = await _apiClient.SubmitBatchAsync(requests, BatchDelayMs);

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

    private async Task AutoSendDataAsync()
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                GenerateRandomTraceCode();
                RandomizeResult();
                UpdateCheckTime();
            });

            var request = Application.Current.Dispatcher.Invoke(() => BuildCurrentRequest());
            _apiClient.SetBaseUrl(ServerUrl);
            var result = await _apiClient.SubmitLabViewDataAsync(request);

            Application.Current.Dispatcher.Invoke(() =>
            {
                AddSubmitResult(result);
                UpdateStatistics();
                LastResponseJson = result.ResponseMessage;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"自動發送失敗: {ex.Message}");
        }
    }

    #endregion

    #region 歷史記錄

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

    [RelayCommand]
    private void ViewHistoryDetail()
    {
        if (SelectedHistoryItem != null)
        {
            MessageBox.Show(
                $"TraceCode: {SelectedHistoryItem.TraceCode}\n" +
                $"時間: {SelectedHistoryItem.SentTimeText}\n" +
                $"狀態: {SelectedHistoryItem.StatusText}\n" +
                $"耗時: {SelectedHistoryItem.ElapsedMs}ms\n\n" +
                $"請求 JSON:\n{SelectedHistoryItem.RequestJson}\n\n" +
                $"錯誤訊息: {SelectedHistoryItem.ErrorMessage}",
                "詳細資訊", MessageBoxButton.OK, MessageBoxImage.Information);
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
            IsSuccess = result.IsSuccess,
            RequestJson = result.RequestJson
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

    #endregion

    #region 輔助方法

    private LabViewInspectionRequest BuildCurrentRequest()
    {
        return _apiClient.BuildLabViewRequest(
            ProcName, DevName, UserName, WorkClass,
            TraceCode, LotNo, PartNumber, Remark,
            ParamDataItems, BenchmarkItems, OtherDataItems);
    }

    [RelayCommand]
    private void UpdatePreviewJson()
    {
        try
        {
            var request = BuildCurrentRequest();
            PreviewJson = _apiClient.GetPreviewJson(request);
        }
        catch (Exception ex)
        {
            PreviewJson = $"Error: {ex.Message}";
        }
    }

    #endregion
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
    public string RequestJson { get; set; } = string.Empty;

    public string SentTimeText => SentTime.ToString("HH:mm:ss");
}
