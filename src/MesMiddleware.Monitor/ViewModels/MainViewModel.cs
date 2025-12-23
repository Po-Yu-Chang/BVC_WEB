using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Services;
using System.IO;
using System.Reflection;
using System.Windows;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// 監控儀表板的主視圖模型
/// 協調子視圖模型並管理系統匣功能
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>
    /// 狀態視圖模型
    /// </summary>
    [ObservableProperty]
    private StatusViewModel _statusViewModel;

    /// <summary>
    /// 歷史記錄視圖模型
    /// </summary>
    [ObservableProperty]
    private HistoryViewModel _historyViewModel;

    /// <summary>
    /// 佇列狀態視圖模型
    /// </summary>
    [ObservableProperty]
    private QueueViewModel _queueViewModel;

    /// <summary>
    /// 追溯碼校驗視圖模型
    /// </summary>
    [ObservableProperty]
    private TraceVerificationViewModel _traceVerificationViewModel;

    /// <summary>
    /// 視窗是否可見
    /// </summary>
    [ObservableProperty]
    private bool _isWindowVisible = true;

    /// <summary>
    /// 當前語言代碼
    /// </summary>
    [ObservableProperty]
    private string _currentLanguage = "zh-TW";

    /// <summary>
    /// 本地化服務
    /// </summary>
    public ILocalizationService LocalizationService => _localizationService;

    // 視窗標題
    public string WindowTitle => _localizationService.GetString("WindowTitle");

    // Tab 標題
    public string TabStatus => _localizationService.GetString("TabStatus");
    public string TabHistory => _localizationService.GetString("TabHistory");
    public string TabQueue => _localizationService.GetString("TabQueue");
    public string TabCommands => _localizationService.GetString("TabCommands");
    public string TabTraceVerification => _localizationService.GetString("TraceVerification_TabTitle");

    // 語言選項
    public string LanguageTitle => _localizationService.GetString("Language_Title");
    public string LanguageEnglish => _localizationService.GetString("Language_English");
    public string LanguageSimplifiedChinese => _localizationService.GetString("Language_SimplifiedChinese");
    public string LanguageTraditionalChinese => _localizationService.GetString("Language_TraditionalChinese");

    // 系統匣選單
    public string TrayMenuShow => _localizationService.GetString("TrayMenuShow");
    public string TrayMenuExit => _localizationService.GetString("TrayMenuExit");

    // 主標題
    public string HeaderTitle => _localizationService.GetString("Header_Title");
    public string HeaderSubtitle => _localizationService.GetString("Header_Subtitle");

    // 版本資訊
    public const string AppVersion = "v2.2.0";
    public static string BuildDate => GetBuildDate();
    public string VersionInfo => $"{AppVersion} | {BuildDate}";

    private static string GetBuildDate()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                return File.GetLastWriteTime(location).ToString("yyyy-MM-dd");
            }
        }
        catch { }
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    // 服務狀態頁面
    public string StatusSectionTitle => _localizationService.GetString("Status_SectionTitle");
    public string StatusServiceRunning => _localizationService.GetString("Status_ServiceRunning");
    public string StatusWebApiConnection => _localizationService.GetString("Status_WebApiConnection");
    public string StatusSharedMemory => _localizationService.GetString("Status_SharedMemory");
    public string StatusStatisticsTitle => _localizationService.GetString("Status_StatisticsTitle");
    public string StatusTotalReceived => _localizationService.GetString("Status_TotalReceived");
    public string StatusSuccessUploads => _localizationService.GetString("Status_SuccessUploads");
    public string StatusQueuedItems => _localizationService.GetString("Status_QueuedItems");
    public string StatusUptime => _localizationService.GetString("Status_Uptime");
    public string StatusLastUpdate => _localizationService.GetString("Status_LastUpdate");
    public string StatusLastError => _localizationService.GetString("Status_LastError");

    // 佇列狀態頁面
    public string QueueSectionTitle => _localizationService.GetString("Queue_SectionTitle");
    public string QueuePendingTitle => _localizationService.GetString("Queue_PendingTitle");
    public string QueueRetryingTitle => _localizationService.GetString("Queue_RetryingTitle");
    public string QueueFailedTitle => _localizationService.GetString("Queue_FailedTitle");
    public string QueueTotalDepthTitle => _localizationService.GetString("Queue_TotalDepthTitle");
    public string QueueOldestItem => _localizationService.GetString("Queue_OldestItem");

    // 上傳歷史頁面
    public string HistoryRefreshButton => _localizationService.GetString("History_RefreshButton");
    public string HistoryColumnTimestamp => _localizationService.GetString("History_ColumnTimestamp");
    public string HistoryColumnTraceCode => _localizationService.GetString("History_ColumnTraceCode");
    public string HistoryColumnDevice => _localizationService.GetString("History_ColumnDevice");
    public string HistoryColumnStatus => _localizationService.GetString("History_ColumnStatus");
    public string HistoryColumnRetryCount => _localizationService.GetString("History_ColumnRetryCount");
    public string HistoryColumnError => _localizationService.GetString("History_ColumnError");

    // 追溯碼校驗頁面
    public string TraceVerificationPageTitle => _localizationService.GetString("TraceVerification_PageTitle");
    public string TraceVerificationPageDescription => _localizationService.GetString("TraceVerification_PageDescription");
    public string TraceVerificationWorkOrderLabel => _localizationService.GetString("TraceVerification_WorkOrderLabel");
    public string TraceVerificationWorkOrderPlaceholder => _localizationService.GetString("TraceVerification_WorkOrderPlaceholder");
    public string TraceVerificationTraceCodeLabel => _localizationService.GetString("TraceVerification_TraceCodeLabel");
    public string TraceVerificationTraceCodeHint => _localizationService.GetString("TraceVerification_TraceCodeHint");
    public string TraceVerificationTraceCodePlaceholder => _localizationService.GetString("TraceVerification_TraceCodePlaceholder");
    public string TraceVerificationVerifyButton => _localizationService.GetString("TraceVerification_VerifyButton");
    public string TraceVerificationClearButton => _localizationService.GetString("TraceVerification_ClearButton");
    public string TraceVerificationResultTitle => _localizationService.GetString("TraceVerification_ResultTitle");
    public string TraceVerificationStatisticsTitle => _localizationService.GetString("TraceVerification_StatisticsTitle");
    public string TraceVerificationSuccessCount => _localizationService.GetString("TraceVerification_SuccessCount");
    public string TraceVerificationFailureCount => _localizationService.GetString("TraceVerification_FailureCount");
    public string TraceVerificationLastVerification => _localizationService.GetString("TraceVerification_LastVerification");
    public string TraceVerificationNoVerification => _localizationService.GetString("TraceVerification_NoVerification");
    public string TraceVerificationResetStatistics => _localizationService.GetString("TraceVerification_ResetStatistics");

    // 歷史資料頁面
    public string HistoryEquipmentToMiddlewareTab => _localizationService.GetString("History_EquipmentToMiddlewareTab");
    public string HistoryMiddlewareToMesTab => _localizationService.GetString("History_MiddlewareToMesTab");
    public string HistoryHeaderTimestamp => _localizationService.GetString("History_HeaderTimestamp");
    public string HistoryHeaderUploadTime => _localizationService.GetString("History_HeaderUploadTime");
    public string HistoryHeaderTraceCode => _localizationService.GetString("History_HeaderTraceCode");
    public string HistoryHeaderEquipmentName => _localizationService.GetString("History_HeaderEquipmentName");
    public string HistoryHeaderStatus => _localizationService.GetString("History_HeaderStatus");
    public string HistoryHeaderErrorMessage => _localizationService.GetString("History_HeaderErrorMessage");

    /// <summary>
    /// 建構函式，初始化主視圖模型
    /// </summary>
    public MainViewModel(
        IMiddlewareApiClient apiClient,
        ILocalizationService localizationService,
        StatusViewModel statusViewModel,
        HistoryViewModel historyViewModel,
        QueueViewModel queueViewModel,
        TraceVerificationViewModel traceVerificationViewModel,
        ILogger<MainViewModel> logger)
    {
        _apiClient = apiClient;
        _localizationService = localizationService;
        StatusViewModel = statusViewModel;
        HistoryViewModel = historyViewModel;
        QueueViewModel = queueViewModel;
        TraceVerificationViewModel = traceVerificationViewModel;
        _logger = logger;

        // 訂閱語言變更事件
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// 顯示視窗命令
    /// </summary>
    [RelayCommand]
    private void ShowWindow()
    {
        IsWindowVisible = true;
        _logger.LogInformation("Window shown from system tray");
    }

    /// <summary>
    /// 隱藏視窗命令
    /// </summary>
    [RelayCommand]
    private void HideWindow()
    {
        IsWindowVisible = false;
        _logger.LogInformation("Window hidden to system tray");
    }

    /// <summary>
    /// 結束應用程式命令
    /// </summary>
    [RelayCommand]
    private void ExitApplication()
    {
        _logger.LogInformation("Application exit requested");
        Application.Current.Shutdown();
    }

    /// <summary>
    /// 初始化視圖模型，啟動所有子視圖模型的定期更新
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing main view model");

        // 初始化 StatusViewModel（從 SQLite 載入佇列統計資料）
        await StatusViewModel.InitializeAsync();

        // 啟動所有子視圖模型的定期更新
        await StatusViewModel.StartPeriodicRefreshAsync();
        await HistoryViewModel.StartPeriodicRefreshAsync();
        await QueueViewModel.StartPeriodicRefreshAsync();
    }

    /// <summary>
    /// 關閉視圖模型，停止所有定期更新
    /// </summary>
    public async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down main view model");

        // 取消訂閱語言變更事件
        _localizationService.LanguageChanged -= OnLanguageChanged;

        // 停止定期更新
        StatusViewModel.StopPeriodicRefresh();
        HistoryViewModel.StopPeriodicRefresh();
        QueueViewModel.StopPeriodicRefresh();

        await Task.CompletedTask;
    }

    /// <summary>
    /// 切換語言命令
    /// </summary>
    /// <param name="languageCode">語言代碼（en, zh-CN, zh-TW）</param>
    [RelayCommand]
    private void ChangeLanguage(string languageCode)
    {
        _logger.LogInformation("Changing language to: {LanguageCode}", languageCode);
        _localizationService.ChangeLanguage(languageCode);
        CurrentLanguage = languageCode;
    }

    /// <summary>
    /// 語言變更事件處理
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _logger.LogInformation("Language changed to: {Culture}", _localizationService.CurrentCulture.Name);

        // 通知所有屬性變更，以更新 UI
        OnPropertyChanged(nameof(LocalizationService));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(TabStatus));
        OnPropertyChanged(nameof(TabHistory));
        OnPropertyChanged(nameof(TabQueue));
        OnPropertyChanged(nameof(TabCommands));
        OnPropertyChanged(nameof(LanguageTitle));
        OnPropertyChanged(nameof(LanguageEnglish));
        OnPropertyChanged(nameof(LanguageSimplifiedChinese));
        OnPropertyChanged(nameof(LanguageTraditionalChinese));
        OnPropertyChanged(nameof(TrayMenuShow));
        OnPropertyChanged(nameof(TrayMenuExit));
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(StatusSectionTitle));
        OnPropertyChanged(nameof(StatusServiceRunning));
        OnPropertyChanged(nameof(StatusWebApiConnection));
        OnPropertyChanged(nameof(StatusSharedMemory));
        OnPropertyChanged(nameof(StatusStatisticsTitle));
        OnPropertyChanged(nameof(StatusTotalReceived));
        OnPropertyChanged(nameof(StatusSuccessUploads));
        OnPropertyChanged(nameof(StatusQueuedItems));
        OnPropertyChanged(nameof(StatusUptime));
        OnPropertyChanged(nameof(StatusLastUpdate));
        OnPropertyChanged(nameof(StatusLastError));
        OnPropertyChanged(nameof(QueueSectionTitle));
        OnPropertyChanged(nameof(QueuePendingTitle));
        OnPropertyChanged(nameof(QueueRetryingTitle));
        OnPropertyChanged(nameof(QueueFailedTitle));
        OnPropertyChanged(nameof(QueueTotalDepthTitle));
        OnPropertyChanged(nameof(QueueOldestItem));
        OnPropertyChanged(nameof(HistoryRefreshButton));
        OnPropertyChanged(nameof(HistoryColumnTimestamp));
        OnPropertyChanged(nameof(HistoryColumnTraceCode));
        OnPropertyChanged(nameof(HistoryColumnDevice));
        OnPropertyChanged(nameof(HistoryColumnStatus));
        OnPropertyChanged(nameof(HistoryColumnRetryCount));
        OnPropertyChanged(nameof(HistoryColumnError));
        OnPropertyChanged(nameof(TabTraceVerification));
        OnPropertyChanged(nameof(TraceVerificationPageTitle));
        OnPropertyChanged(nameof(TraceVerificationPageDescription));
        OnPropertyChanged(nameof(TraceVerificationWorkOrderLabel));
        OnPropertyChanged(nameof(TraceVerificationWorkOrderPlaceholder));
        OnPropertyChanged(nameof(TraceVerificationTraceCodeLabel));
        OnPropertyChanged(nameof(TraceVerificationTraceCodeHint));
        OnPropertyChanged(nameof(TraceVerificationTraceCodePlaceholder));
        OnPropertyChanged(nameof(TraceVerificationVerifyButton));
        OnPropertyChanged(nameof(TraceVerificationClearButton));
        OnPropertyChanged(nameof(TraceVerificationResultTitle));
        OnPropertyChanged(nameof(TraceVerificationStatisticsTitle));
        OnPropertyChanged(nameof(TraceVerificationSuccessCount));
        OnPropertyChanged(nameof(TraceVerificationFailureCount));
        OnPropertyChanged(nameof(TraceVerificationLastVerification));
        OnPropertyChanged(nameof(TraceVerificationNoVerification));
        OnPropertyChanged(nameof(TraceVerificationResetStatistics));
        OnPropertyChanged(nameof(HistoryEquipmentToMiddlewareTab));
        OnPropertyChanged(nameof(HistoryMiddlewareToMesTab));
        OnPropertyChanged(nameof(HistoryHeaderTimestamp));
        OnPropertyChanged(nameof(HistoryHeaderUploadTime));
        OnPropertyChanged(nameof(HistoryHeaderTraceCode));
        OnPropertyChanged(nameof(HistoryHeaderEquipmentName));
        OnPropertyChanged(nameof(HistoryHeaderStatus));
        OnPropertyChanged(nameof(HistoryHeaderErrorMessage));
    }
}
