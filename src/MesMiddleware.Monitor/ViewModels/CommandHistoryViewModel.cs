using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// View model for command history display (T066).
/// Shows equipment commands with status, timestamps, and acknowledgments.
/// Part of User Story 3 (Bidirectional Command & Control).
/// </summary>
public partial class CommandHistoryViewModel : ObservableObject
{
    private readonly IMiddlewareApiClient _apiClient;
    private readonly ILogger<CommandHistoryViewModel> _logger;
    private CancellationTokenSource? _refreshCts;
    private Task? _refreshTask;

    [ObservableProperty]
    private ObservableCollection<CommandRecord> _commandRecords = new();

    [ObservableProperty]
    private ObservableCollection<CommandRecord> _filteredRecords = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public CommandHistoryViewModel(IMiddlewareApiClient apiClient, ILogger<CommandHistoryViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            _logger.LogDebug("Loading command history from middleware API");

            // Get command history from middleware (max 1000 records)
            var commands = await _apiClient.GetCommandHistoryAsync(maxRecords: 1000);

            CommandRecords.Clear();
            foreach (var cmd in commands)
            {
                CommandRecords.Add(cmd);
            }

            _logger.LogInformation("Loaded {Count} command records", CommandRecords.Count);

            // Apply current filter
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

    [RelayCommand]
    private void ApplyFilter()
    {
        FilteredRecords.Clear();

        var filtered = CommandRecords.AsEnumerable();

        // Filter by text (CommandType or EquipmentName)
        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            filtered = filtered.Where(r =>
                r.CommandType.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                r.EquipmentName.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by status
        if (StatusFilter != "All")
        {
            filtered = filtered.Where(r => r.Status == StatusFilter);
        }

        foreach (var record in filtered)
        {
            FilteredRecords.Add(record);
        }

        _logger.LogDebug("Filtered {Count} records from {Total} total",
            FilteredRecords.Count, CommandRecords.Count);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
        StatusFilter = "All";
        ApplyFilter();
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
        _refreshTask?.Wait(TimeSpan.FromSeconds(5));
        _refreshCts?.Dispose();
    }

    private async Task PeriodicRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await LoadHistoryAsync();
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

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnStatusFilterChanged(string value)
    {
        ApplyFilter();
    }
}
