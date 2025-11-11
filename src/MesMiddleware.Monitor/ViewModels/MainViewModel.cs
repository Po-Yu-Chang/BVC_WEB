using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Services;
using System.Windows;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// Main view model for the monitoring dashboard.
/// Coordinates child view models and manages system tray.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private StatusViewModel _statusViewModel;

    [ObservableProperty]
    private HistoryViewModel _historyViewModel;

    [ObservableProperty]
    private QueueViewModel _queueViewModel;

    [ObservableProperty]
    private bool _isWindowVisible = true;

    public MainViewModel(
        IMiddlewareApiClient apiClient,
        StatusViewModel statusViewModel,
        HistoryViewModel historyViewModel,
        QueueViewModel queueViewModel,
        ILogger<MainViewModel> logger)
    {
        _apiClient = apiClient;
        StatusViewModel = statusViewModel;
        HistoryViewModel = historyViewModel;
        QueueViewModel = queueViewModel;
        _logger = logger;
    }

    [RelayCommand]
    private void ShowWindow()
    {
        IsWindowVisible = true;
        _logger.LogInformation("Window shown from system tray");
    }

    [RelayCommand]
    private void HideWindow()
    {
        IsWindowVisible = false;
        _logger.LogInformation("Window hidden to system tray");
    }

    [RelayCommand]
    private void ExitApplication()
    {
        _logger.LogInformation("Application exit requested");
        Application.Current.Shutdown();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing main view model");

        // Start periodic refresh for all child view models
        await StatusViewModel.StartPeriodicRefreshAsync();
        await HistoryViewModel.StartPeriodicRefreshAsync();
        await QueueViewModel.StartPeriodicRefreshAsync();
    }

    public async Task ShutdownAsync()
    {
        _logger.LogInformation("Shutting down main view model");

        // Stop periodic refresh
        StatusViewModel.StopPeriodicRefresh();
        HistoryViewModel.StopPeriodicRefresh();
        QueueViewModel.StopPeriodicRefresh();

        await Task.CompletedTask;
    }
}
