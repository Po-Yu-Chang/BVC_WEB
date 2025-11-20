using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MesMiddleware.Simulator.Data;
using MesMiddleware.Simulator.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace MesMiddleware.Simulator.ViewModels;

/// <summary>
/// 主視圖模型
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly WebApiHost _webApiHost;
    private System.Timers.Timer? _refreshTimer;

    [ObservableProperty]
    private bool _isServerRunning;

    [ObservableProperty]
    private string _serverUrl = "http://localhost:5200";

    [ObservableProperty]
    private int _totalRequests;

    [ObservableProperty]
    private int _successCount;

    [ObservableProperty]
    private int _failureCount;

    [ObservableProperty]
    private int _deviceLoginCount;

    [ObservableProperty]
    private int _traceDataCount;

    [ObservableProperty]
    private ObservableCollection<RequestLogItemViewModel> _requestLogs = new();

    [ObservableProperty]
    private RequestLogItemViewModel? _selectedRequestLog;

    [ObservableProperty]
    private string _simulationMode = "Normal";

    [ObservableProperty]
    private int _delayMs = 0;

    [ObservableProperty]
    private int _failureRate = 0;

    [ObservableProperty]
    private string _testToken = "尚未生成 (請先啟動服務器並生成測試 Token)";

    public MainViewModel(WebApiHost webApiHost)
    {
        _webApiHost = webApiHost;
    }

    [RelayCommand]
    private async Task StartServerAsync()
    {
        try
        {
            await _webApiHost.StartAsync(ServerUrl);
            IsServerRunning = true;

            // 啟動定時刷新
            _refreshTimer = new System.Timers.Timer(2000); // 每2秒刷新
            _refreshTimer.Elapsed += async (s, e) => await RefreshDataAsync();
            _refreshTimer.Start();

            MessageBox.Show($"MES Cloud Simulator 已啟動\n\n監聽地址: {ServerUrl}\nSwagger UI: {ServerUrl}/swagger",
                "服務器啟動", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"啟動失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        try
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshTimer = null;

            await _webApiHost.StopAsync();
            IsServerRunning = false;

            MessageBox.Show("MES Cloud Simulator 已停止", "服務器停止",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"停止失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (!IsServerRunning) return;

        try
        {
            using var scope = _webApiHost.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SimulatorDbContext>();

            // 更新統計數據
            TotalRequests = await dbContext.RequestLogs.CountAsync();
            SuccessCount = await dbContext.RequestLogs.CountAsync(r => r.IsSuccess);
            FailureCount = await dbContext.RequestLogs.CountAsync(r => !r.IsSuccess);
            DeviceLoginCount = await dbContext.DeviceLogins.CountAsync(d => d.IsActive);
            TraceDataCount = await dbContext.TraceDataRecords.CountAsync();

            // 更新請求日誌列表 (最新 100 條)
            var logs = await dbContext.RequestLogs
                .OrderByDescending(r => r.Timestamp)
                .Take(100)
                .Select(r => new RequestLogItemViewModel
                {
                    Id = r.Id,
                    Timestamp = r.Timestamp,
                    Method = r.Method,
                    Endpoint = r.Endpoint,
                    ClientIp = r.ClientIp,
                    ResponseStatusCode = r.ResponseStatusCode,
                    ProcessingTimeMs = r.ProcessingTimeMs,
                    IsSuccess = r.IsSuccess,
                    RequestHeaders = r.RequestHeaders,
                    RequestBody = r.RequestBody,
                    ResponseBody = r.ResponseBody,
                    ErrorMessage = r.ErrorMessage
                })
                .ToListAsync();

            // 使用 UI 線程更新
            Application.Current.Dispatcher.Invoke(() =>
            {
                RequestLogs.Clear();
                foreach (var log in logs)
                {
                    RequestLogs.Add(log);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"刷新數據失敗: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ApplySimulationSettings()
    {
        var configService = _webApiHost.GetConfigService();
        if (configService == null) return;

        configService.Mode = SimulationMode switch
        {
            "Normal" => Services.SimulationMode.Normal,
            "AlwaysFail" => Services.SimulationMode.AlwaysFail,
            "Random" => Services.SimulationMode.Random,
            "Delayed" => Services.SimulationMode.Delayed,
            _ => Services.SimulationMode.Normal
        };

        configService.DelayMs = DelayMs;
        configService.FailureRate = FailureRate;

        MessageBox.Show("模擬設定已更新", "設定", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task ClearLogsAsync()
    {
        var result = MessageBox.Show("確定要清空所有日誌嗎?", "確認",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            using var scope = _webApiHost.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SimulatorDbContext>();

            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RequestLogs");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM DeviceLogins");
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM TraceDataRecords");

            await RefreshDataAsync();

            MessageBox.Show("日誌已清空", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private async Task GenerateTestTokenAsync()
    {
        if (!IsServerRunning) return;

        try
        {
            using var scope = _webApiHost.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SimulatorDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<Services.TokenService>();

            // 生成測試 Token
            var testMachineNo = "TEST-MACHINE-001";
            var testIp = "192.168.1.100";

            // 檢查是否已經存在測試設備
            var existingDevice = await dbContext.DeviceLogins
                .FirstOrDefaultAsync(d => d.PrtMacNo == testMachineNo && d.IsActive);

            string token;
            if (existingDevice != null)
            {
                token = existingDevice.Token;
                existingDevice.LastUsedTime = DateTime.Now;
            }
            else
            {
                // 創建新的測試設備登錄
                token = tokenService.GenerateToken();
                var newDevice = new Models.DeviceLogin
                {
                    PrtMacNo = testMachineNo,
                    IpAddr = testIp,
                    Token = token,
                    LoginTime = DateTime.Now,
                    LastUsedTime = DateTime.Now,
                    IsActive = true
                };
                dbContext.DeviceLogins.Add(newDevice);
            }

            await dbContext.SaveChangesAsync();

            TestToken = token;

            MessageBox.Show($"測試 Token 已生成!\n\n機台編號: {testMachineNo}\nIP 地址: {testIp}\n\nToken 已複製到剪貼簿",
                "Token 生成成功", MessageBoxButton.OK, MessageBoxImage.Information);

            // 自動複製到剪貼簿
            Clipboard.SetText(token);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"生成 Token 失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CopyToken()
    {
        if (string.IsNullOrEmpty(TestToken) || TestToken.StartsWith("尚未生成"))
        {
            MessageBox.Show("尚未生成測試 Token,請先點擊「生成新 Token」按鈕", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Clipboard.SetText(TestToken);
            MessageBox.Show("Token 已複製到剪貼簿!", "複製成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"複製失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
