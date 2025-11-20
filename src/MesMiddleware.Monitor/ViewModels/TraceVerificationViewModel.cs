using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// 追溯碼校驗視圖模型
/// 用於掃描工單號和板號，並呼叫 MES API 驗證是否混批
/// </summary>
public partial class TraceVerificationViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<TraceVerificationViewModel> _logger;

    /// <summary>
    /// 工單號
    /// </summary>
    [ObservableProperty]
    private string _workOrderNumber = string.Empty;

    /// <summary>
    /// 板號/追溯碼（可輸入多個，用逗號或換行分隔）
    /// </summary>
    [ObservableProperty]
    private string _traceCodes = string.Empty;

    /// <summary>
    /// 驗證結果訊息
    /// </summary>
    [ObservableProperty]
    private string _resultMessage = string.Empty;

    /// <summary>
    /// 驗證結果狀態（success, error, idle）
    /// </summary>
    [ObservableProperty]
    private string _resultStatus = "idle";

    /// <summary>
    /// 是否正在驗證
    /// </summary>
    [ObservableProperty]
    private bool _isVerifying;

    /// <summary>
    /// 驗證成功次數
    /// </summary>
    [ObservableProperty]
    private int _successCount;

    /// <summary>
    /// 驗證失敗次數
    /// </summary>
    [ObservableProperty]
    private int _failureCount;

    /// <summary>
    /// 最後驗證時間
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastVerificationTime;

    /// <summary>
    /// 建構函式
    /// </summary>
    public TraceVerificationViewModel(IMiddlewareApiClient apiClient, ILogger<TraceVerificationViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// 驗證追溯碼命令
    /// </summary>
    [RelayCommand]
    private async Task VerifyAsync()
    {
        // 驗證輸入
        if (string.IsNullOrWhiteSpace(WorkOrderNumber))
        {
            ResultMessage = "請輸入工單號";
            ResultStatus = "error";
            return;
        }

        if (string.IsNullOrWhiteSpace(TraceCodes))
        {
            ResultMessage = "請輸入板號/追溯碼";
            ResultStatus = "error";
            return;
        }

        IsVerifying = true;
        ResultMessage = "正在驗證...";
        ResultStatus = "idle";

        try
        {
            // 解析追溯碼（支援逗號、分號、換行分隔）
            var codes = TraceCodes
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (codes.Count == 0)
            {
                ResultMessage = "請輸入有效的板號/追溯碼";
                ResultStatus = "error";
                return;
            }

            _logger.LogInformation("驗證追溯碼: 工單={WorkOrder}, 板號數量={Count}", WorkOrderNumber, codes.Count);

            // 呼叫 API 驗證
            var result = await _apiClient.VerifyTraceCodesAsync(WorkOrderNumber, codes);

            LastVerificationTime = DateTime.Now;

            if (result.Success)
            {
                ResultMessage = $"✓ 驗證成功\n工單: {WorkOrderNumber}\n板號數量: {codes.Count}";
                ResultStatus = "success";
                SuccessCount++;

                _logger.LogInformation("追溯碼驗證成功: {WorkOrder}", WorkOrderNumber);

                // 清空輸入框，準備下一次掃描
                TraceCodes = string.Empty;
            }
            else
            {
                ResultMessage = $"✗ 驗證失敗\n{result.Message}";
                ResultStatus = "error";
                FailureCount++;

                _logger.LogWarning("追溯碼驗證失敗: {WorkOrder}, 錯誤: {Error}", WorkOrderNumber, result.Message);
            }
        }
        catch (Exception ex)
        {
            ResultMessage = $"✗ 驗證失敗\n錯誤: {ex.Message}";
            ResultStatus = "error";
            FailureCount++;

            _logger.LogError(ex, "追溯碼驗證發生例外: {WorkOrder}", WorkOrderNumber);
        }
        finally
        {
            IsVerifying = false;
        }
    }

    /// <summary>
    /// 清除輸入命令
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        WorkOrderNumber = string.Empty;
        TraceCodes = string.Empty;
        ResultMessage = string.Empty;
        ResultStatus = "idle";
    }

    /// <summary>
    /// 重置統計命令
    /// </summary>
    [RelayCommand]
    private void ResetStatistics()
    {
        SuccessCount = 0;
        FailureCount = 0;
        LastVerificationTime = null;
        ResultMessage = "統計已重置";
        ResultStatus = "idle";
    }

    /// <summary>
    /// 工單號 TextBox Enter 鍵處理（移動焦點到板號輸入框）
    /// </summary>
    partial void OnWorkOrderNumberChanged(string value)
    {
        // 當工單號輸入完成，可觸發焦點移動（由 View 處理）
    }
}
