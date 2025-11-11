using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;
using System.Collections.ObjectModel;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// View model for upload history display.
/// Shows recent successful and failed uploads.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<HistoryViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    [ObservableProperty]
    private ObservableCollection<UploadRecord> _uploadRecords = new();

    [ObservableProperty]
    private ObservableCollection<UploadRecord> _filteredRecords = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _statusFilter = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // Legacy
    [ObservableProperty]
    private ObservableCollection<UploadHistoryItem> _historyItems = new();

    [ObservableProperty]
    private string _lastError = string.Empty;

    public HistoryViewModel(IMiddlewareApiClient apiClient, ILogger<HistoryViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task LoadHistoryAsync()
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        try
        {
            var history = await _apiClient.GetUploadHistoryAsync(maxRecords: 1000);

            UploadRecords.Clear();
            foreach (var item in history)
            {
                UploadRecords.Add(item);
            }

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

    public void ApplyFilter()
    {
        FilteredRecords.Clear();

        var filtered = UploadRecords.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(r => r.TraceCode.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            filtered = filtered.Where(r => r.Status.Equals(StatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in filtered)
        {
            FilteredRecords.Add(item);
        }
    }

    public void ClearFilter()
    {
        FilterText = string.Empty;
        StatusFilter = string.Empty;

        FilteredRecords.Clear();
        foreach (var item in UploadRecords)
        {
            FilteredRecords.Add(item);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadHistoryAsync();
    }

    public async Task StartPeriodicRefreshAsync()
    {
        _refreshCts = new CancellationTokenSource();
        _refreshTask = PeriodicRefreshLoopAsync(_refreshCts.Token);
        await Task.CompletedTask;
    }

    public void StopPeriodicRefresh()
    {
        _refreshCts?.Cancel();
        // Don't wait for task completion - immediate shutdown
        // _refreshTask?.Wait(TimeSpan.FromSeconds(5));
        _refreshCts?.Dispose();
    }

    private async Task PeriodicRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync();
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in periodic refresh loop");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}
