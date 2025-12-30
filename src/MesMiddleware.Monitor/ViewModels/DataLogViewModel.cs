using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MesMiddleware.Monitor.ViewModels;

/// <summary>
/// 資料記錄項目
/// </summary>
public partial class DataLogEntry : ObservableObject
{
    [ObservableProperty]
    private DateTime _timestamp;

    [ObservableProperty]
    private string _traceCode = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _jsonContent = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 格式化後的 JSON 內容 (加上縮排和換行)
    /// </summary>
    public string FormattedJsonContent
    {
        get
        {
            if (string.IsNullOrWhiteSpace(JsonContent))
                return string.Empty;

            try
            {
                // 解析 JSON 並重新格式化
                using var doc = JsonDocument.Parse(JsonContent);
                return JsonSerializer.Serialize(doc, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            catch
            {
                // 如果解析失敗，返回原始內容
                return JsonContent;
            }
        }
    }
}

/// <summary>
/// 資料記錄視圖模型 - 顯示 LabVIEW 接收的資料和 MES 發送的資料
/// </summary>
public partial class DataLogViewModel : ObservableObject
{
    private readonly ILogger<DataLogViewModel> _logger;
    private readonly string _labViewLogDirectory;
    private readonly string _mesUploadLogDirectory;

    /// <summary>
    /// LabVIEW 接收的資料記錄
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DataLogEntry> _labViewRecords = new();

    /// <summary>
    /// MES 發送的資料記錄
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DataLogEntry> _mesUploadRecords = new();

    /// <summary>
    /// 是否正在載入
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 最後更新時間
    /// </summary>
    [ObservableProperty]
    private DateTime _lastUpdated;

    /// <summary>
    /// LabVIEW 記錄數量
    /// </summary>
    [ObservableProperty]
    private int _labViewRecordCount;

    /// <summary>
    /// MES 記錄數量
    /// </summary>
    [ObservableProperty]
    private int _mesUploadRecordCount;

    /// <summary>
    /// 選中的 LabVIEW 記錄
    /// </summary>
    [ObservableProperty]
    private DataLogEntry? _selectedLabViewRecord;

    /// <summary>
    /// 選中的 MES 上傳記錄
    /// </summary>
    [ObservableProperty]
    private DataLogEntry? _selectedMesUploadRecord;

    public DataLogViewModel(ILogger<DataLogViewModel> logger)
    {
        _logger = logger;
        _labViewLogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "labview-logs");
        _mesUploadLogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mes-upload-logs");
    }

    /// <summary>
    /// 重新整理 LabVIEW 資料記錄
    /// </summary>
    [RelayCommand]
    private async Task RefreshLabViewLogsAsync()
    {
        IsLoading = true;
        try
        {
            await Task.Run(() => LoadLabViewLogs());
            LastUpdated = DateTime.Now;
            _logger.LogInformation("LabVIEW logs refreshed, count: {Count}", LabViewRecordCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh LabVIEW logs");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 重新整理 MES 上傳資料記錄
    /// </summary>
    [RelayCommand]
    private async Task RefreshMesUploadLogsAsync()
    {
        IsLoading = true;
        try
        {
            await Task.Run(() => LoadMesUploadLogs());
            LastUpdated = DateTime.Now;
            _logger.LogInformation("MES upload logs refreshed, count: {Count}", MesUploadRecordCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh MES upload logs");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 重新整理全部資料記錄
    /// </summary>
    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        await RefreshLabViewLogsAsync();
        await RefreshMesUploadLogsAsync();
    }

    /// <summary>
    /// 載入 LabVIEW 記錄檔案
    /// </summary>
    private void LoadLabViewLogs()
    {
        var entries = new List<DataLogEntry>();

        if (!Directory.Exists(_labViewLogDirectory))
        {
            _logger.LogWarning("LabVIEW log directory does not exist: {Directory}", _labViewLogDirectory);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LabViewRecords.Clear();
                LabViewRecordCount = 0;
            });
            return;
        }

        // 取得今天的記錄檔
        var today = DateTime.Now;
        var filename = $"labview-{today:yyyy-MM-dd}.txt";
        var filepath = Path.Combine(_labViewLogDirectory, filename);

        if (File.Exists(filepath))
        {
            entries.AddRange(ParseLabViewLogFile(filepath));
        }

        // 按時間倒序排列，最新的在前面
        entries = entries.OrderByDescending(e => e.Timestamp).Take(100).ToList();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            LabViewRecords.Clear();
            foreach (var entry in entries)
            {
                LabViewRecords.Add(entry);
            }
            LabViewRecordCount = LabViewRecords.Count;
        });
    }

    /// <summary>
    /// 載入 MES 上傳記錄檔案
    /// </summary>
    private void LoadMesUploadLogs()
    {
        var entries = new List<DataLogEntry>();

        if (!Directory.Exists(_mesUploadLogDirectory))
        {
            _logger.LogWarning("MES upload log directory does not exist: {Directory}", _mesUploadLogDirectory);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MesUploadRecords.Clear();
                MesUploadRecordCount = 0;
            });
            return;
        }

        // 取得今天的記錄檔
        var today = DateTime.Now;
        var filename = $"mes-upload-{today:yyyy-MM-dd}.txt";
        var filepath = Path.Combine(_mesUploadLogDirectory, filename);

        if (File.Exists(filepath))
        {
            entries.AddRange(ParseMesUploadLogFile(filepath));
        }

        // 按時間倒序排列，最新的在前面
        entries = entries.OrderByDescending(e => e.Timestamp).Take(100).ToList();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            MesUploadRecords.Clear();
            foreach (var entry in entries)
            {
                MesUploadRecords.Add(entry);
            }
            MesUploadRecordCount = MesUploadRecords.Count;
        });
    }

    /// <summary>
    /// 解析 LabVIEW 記錄檔案
    /// 格式: [2025-12-30 12:34:56.789] [TraceCode: XXX] {json...}
    /// </summary>
    private List<DataLogEntry> ParseLabViewLogFile(string filepath)
    {
        var entries = new List<DataLogEntry>();
        // 更寬鬆的 regex，支援不同的毫秒位數
        var pattern = @"^\[([^\]]+)\]\s*\[TraceCode:\s*([^\]]+)\]\s*(.+)$";
        var regex = new Regex(pattern);

        try
        {
            // 使用 FileShare.ReadWrite 允許讀取正在寫入的檔案
            using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = regex.Match(line);
                if (match.Success)
                {
                    DateTime timestamp;
                    if (!DateTime.TryParse(match.Groups[1].Value, out timestamp))
                    {
                        timestamp = DateTime.Now;
                    }

                    var entry = new DataLogEntry
                    {
                        Timestamp = timestamp,
                        TraceCode = match.Groups[2].Value.Trim(),
                        Status = "Received",
                        JsonContent = match.Groups[3].Value.Trim()
                    };
                    entries.Add(entry);
                }
                else
                {
                    _logger.LogDebug("LabVIEW log line did not match pattern: {Line}", line.Substring(0, Math.Min(100, line.Length)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LabVIEW log file: {File}", filepath);
        }

        return entries;
    }

    /// <summary>
    /// 解析 MES 上傳記錄檔案
    /// 格式: [2025-12-30 12:34:56.789] [SUCCESS/FAILED] [TraceCode: XXX] | Error: ... {json...}
    /// 或: [2025-12-30 12:34:56.789] [SUCCESS/FAILED] [TraceCode: XXX] {json...}
    /// </summary>
    private List<DataLogEntry> ParseMesUploadLogFile(string filepath)
    {
        var entries = new List<DataLogEntry>();
        // 更寬鬆的 regex
        var pattern = @"^\[([^\]]+)\]\s*\[(SUCCESS|FAILED)\]\s*\[TraceCode:\s*([^\]]+)\](.*)$";
        var regex = new Regex(pattern);

        try
        {
            // 使用 FileShare.ReadWrite 允許讀取正在寫入的檔案
            using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = regex.Match(line);
                if (match.Success)
                {
                    DateTime timestamp;
                    if (!DateTime.TryParse(match.Groups[1].Value, out timestamp))
                    {
                        timestamp = DateTime.Now;
                    }

                    var remaining = match.Groups[4].Value.Trim();
                    var errorMessage = string.Empty;
                    var jsonContent = remaining;

                    // 檢查是否有 "| Error:" 前綴
                    if (remaining.StartsWith("| Error:"))
                    {
                        // 找到 JSON 開始的位置 (第一個 '{')
                        var jsonStart = remaining.IndexOf('{');
                        if (jsonStart > 0)
                        {
                            errorMessage = remaining.Substring(8, jsonStart - 8).Trim(); // 跳過 "| Error:"
                            jsonContent = remaining.Substring(jsonStart).Trim();
                        }
                        else
                        {
                            errorMessage = remaining.Substring(8).Trim();
                            jsonContent = string.Empty;
                        }
                    }
                    else if (remaining.StartsWith("{"))
                    {
                        // 直接是 JSON
                        jsonContent = remaining;
                    }

                    var entry = new DataLogEntry
                    {
                        Timestamp = timestamp,
                        Status = match.Groups[2].Value,
                        TraceCode = match.Groups[3].Value.Trim(),
                        ErrorMessage = errorMessage,
                        JsonContent = jsonContent
                    };
                    entries.Add(entry);
                }
                else
                {
                    _logger.LogDebug("MES upload log line did not match pattern: {Line}", line.Substring(0, Math.Min(100, line.Length)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse MES upload log file: {File}", filepath);
        }

        return entries;
    }
}
