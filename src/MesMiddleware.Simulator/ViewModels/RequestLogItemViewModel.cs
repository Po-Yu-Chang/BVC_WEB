using CommunityToolkit.Mvvm.ComponentModel;

namespace MesMiddleware.Simulator.ViewModels;

/// <summary>
/// 請求日誌項目 ViewModel
/// </summary>
public partial class RequestLogItemViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private DateTime _timestamp;

    [ObservableProperty]
    private string _method = string.Empty;

    [ObservableProperty]
    private string _endpoint = string.Empty;

    [ObservableProperty]
    private string _clientIp = string.Empty;

    [ObservableProperty]
    private int _responseStatusCode;

    [ObservableProperty]
    private long _processingTimeMs;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private string _requestHeaders = string.Empty;

    [ObservableProperty]
    private string _requestBody = string.Empty;

    [ObservableProperty]
    private string _responseBody = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public string StatusText => IsSuccess ? "成功" : "失敗";
    public string StatusColor => IsSuccess ? "Green" : "Red";
}
