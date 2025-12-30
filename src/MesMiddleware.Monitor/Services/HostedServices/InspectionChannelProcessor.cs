using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MesMiddleware.Monitor.Controllers;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;
using MesMiddleware.Shared.Models;
using MesMiddleware.Shared.Models.LabView;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

namespace MesMiddleware.Monitor.Services.HostedServices;

/// <summary>
/// Background service that processes inspection data from in-memory channel.
/// Reads LabViewInspectionRequest from channel and forwards directly to MES Cloud API.
/// No conversion is performed - the original LabVIEW JSON is forwarded as-is.
/// Failed uploads are saved to SQLite queue for automatic retry.
/// </summary>
public class InspectionChannelProcessor : BackgroundService
{
    private readonly Channel<LabViewInspectionRequest> _inspectionChannel;
    private readonly ITokenService _tokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InspectionChannelProcessor> _logger;
    private readonly WebApiOptions _webApiOptions;

    // MES 上傳資料記錄相關
    private static readonly object _logLock = new object();
    private static readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mes-upload-logs");
    private static DateTime _lastCleanup = DateTime.MinValue;
    private static readonly int _retentionDays = 7;

    public InspectionChannelProcessor(
        Channel<LabViewInspectionRequest> inspectionChannel,
        ITokenService tokenService,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<InspectionChannelProcessor> logger,
        IOptions<WebApiOptions> webApiOptions)
    {
        _inspectionChannel = inspectionChannel;
        _tokenService = tokenService;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _webApiOptions = webApiOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inspection channel processor starting...");

        try
        {
            // Check MES Cloud connectivity on startup
            var connectionOk = await CheckMesCloudConnectionAsync(stoppingToken);
            if (connectionOk)
            {
                _logger.LogInformation("MES Cloud connection check: OK");
            }
            else
            {
                _logger.LogWarning("MES Cloud connection check failed - will continue trying to upload");
            }

            // Process channel messages until cancellation
            await foreach (var request in _inspectionChannel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessLabViewRequestAsync(request, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Inspection channel processor is stopping (cancellation requested)");
        }
        catch (System.Threading.Channels.ChannelClosedException)
        {
            _logger.LogInformation("Inspection channel processor is stopping (channel closed)");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in inspection channel processor");
            throw;
        }
        finally
        {
            _logger.LogInformation("Inspection channel processor stopped");
        }
    }

    private async Task<bool> CheckMesCloudConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);
            return !string.IsNullOrEmpty(token);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 處理 LabVIEW 請求，直接轉發到 MES Cloud（不做任何轉換）
    /// </summary>
    private async Task ProcessLabViewRequestAsync(LabViewInspectionRequest request, CancellationToken cancellationToken)
    {
        StatusController.IncrementReceived();
        StatusController.UpdateLastActivity();

        var firstData = request.Data.FirstOrDefault();
        var traceInfo = firstData?.TraceCode ?? firstData?.LotNo ?? "N/A";

        _logger.LogInformation("Processing LabVIEW request: {TraceCodeOrLot} (Count: {Count})",
            traceInfo, request.Data.Count);

        try
        {
            // 直接轉發原始請求到 MES Cloud API
            var (uploadSucceeded, errorMessage) = await UploadToMesCloudAsync(request, cancellationToken);

            if (uploadSucceeded)
            {
                StatusController.IncrementSuccessful();
                // 記錄到 Monitor → MES Cloud 歷史
                StatusController.AddMonitorToCloudHistory(traceInfo, "Success", null);

                // 記錄到資料庫歷史
                using (var scope = _serviceProvider.CreateScope())
                {
                    var queueService = scope.ServiceProvider.GetRequiredService<IUploadQueueService>();
                    await queueService.AddHistoryAsync(
                        firstData?.TraceCode,
                        firstData?.LotNo,
                        "Success",
                        null,
                        0,
                        "Device",
                        null,
                        cancellationToken);
                }
                _logger.LogInformation("Successfully uploaded LabVIEW data for {TraceCodeOrLot}", traceInfo);
            }
            else
            {
                _logger.LogWarning("Upload failed for {TraceCodeOrLot}, error: {Error}. Saving to queue...",
                    traceInfo, errorMessage);

                StatusController.IncrementQueued();
                StatusController.AddMonitorToCloudHistory(traceInfo, "Queued", "Upload failed - saved to queue for retry");

                // 儲存到 SQLite 佇列等待重試（需要轉換為 InspectionRecord 格式）
                await SaveToQueueAsync(request, errorMessage ?? "MES Cloud upload failed", cancellationToken);

                _logger.LogWarning("Failed to upload LabVIEW data for {TraceCodeOrLot} - saved to SQLite queue for retry", traceInfo);
            }
        }
        catch (Exception ex)
        {
            StatusController.IncrementQueued();
            StatusController.AddMonitorToCloudHistory(traceInfo, "Queued", $"Exception: {ex.Message} - saved to queue for retry");

            // 儲存到 SQLite 佇列等待重試
            await SaveToQueueAsync(request, ex.Message, cancellationToken);

            _logger.LogError(ex, "Failed to upload LabVIEW data for {TraceCodeOrLot} - saved to SQLite queue for retry", traceInfo);
        }
    }

    /// <summary>
    /// 直接轉發 LabVIEW 請求到 MES Cloud（不做任何轉換）
    /// </summary>
    private async Task<(bool success, string? errorMessage)> UploadToMesCloudAsync(LabViewInspectionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Get authentication token
            var token = await _tokenService.GetAccessTokenAsync(cancellationToken);

            var httpClient = _httpClientFactory.CreateClient("MesCloudClient");

            // 直接轉發原始 LabVIEW 請求，不做任何轉換
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3")
            {
                Content = JsonContent.Create(request, options: new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };

            // Add accessToken header
            httpRequest.Headers.Add("accessToken", token);

            // Send request
            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            // Handle 401 Unauthorized - refresh token and retry
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized from MES Cloud, refreshing token and retrying");
                await _tokenService.RefreshTokenAsync(cancellationToken);

                // Retry with new token
                token = await _tokenService.GetAccessTokenAsync(cancellationToken);

                var retryRequest = new HttpRequestMessage(HttpMethod.Post, "/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3")
                {
                    Content = JsonContent.Create(request, options: new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    })
                };
                retryRequest.Headers.Add("accessToken", token);

                response = await httpClient.SendAsync(retryRequest, cancellationToken);
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("MES Cloud upload successful: {StatusCode}", response.StatusCode);
                LogMesUploadToFile(request, true, null);
                return (true, null);
            }
            else
            {
                var errorMsg = $"MES Cloud returned {response.StatusCode}: {responseContent}";
                _logger.LogWarning(errorMsg);
                LogMesUploadToFile(request, false, errorMsg);
                return (false, errorMsg);
            }
        }
        catch (HttpRequestException ex)
        {
            var errorMsg = $"HTTP request failed: {ex.Message}";
            _logger.LogError(ex, "HTTP request exception during MES Cloud upload");
            LogMesUploadToFile(request, false, errorMsg);
            return (false, errorMsg);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Exception during MES Cloud upload: {ex.Message}";
            _logger.LogError(ex, errorMsg);
            LogMesUploadToFile(request, false, errorMsg);
            return (false, errorMsg);
        }
    }

    /// <summary>
    /// 將失敗的請求儲存到 SQLite 佇列（需要轉換為 InspectionRecord 格式）
    /// </summary>
    private async Task SaveToQueueAsync(LabViewInspectionRequest request, string errorMessage, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IUploadQueueService>();

        // 為每筆 data 項目建立 InspectionRecord 並儲存到佇列
        foreach (var data in request.Data)
        {
            var record = new InspectionRecord
            {
                ProcName = data.ProcName ?? string.Empty,
                DevName = data.DevName ?? string.Empty,
                UserName = data.UserName ?? string.Empty,
                WorkClass = data.WorkClass ?? string.Empty,
                TraceCode = data.TraceCode,
                LotNo = data.LotNo,
                PartNumber = data.PartNumber,
                Remark = data.Remark,
                InspectionTime = DateTime.Now
            };

            // 複製 ParamData
            foreach (var param in data.ParamData)
            {
                record.ParamData.Add(new ParamDataItem
                {
                    Code = param.Code,
                    Name = param.Name ?? param.Code ?? string.Empty,
                    Value = param.Value ?? string.Empty,
                    Unit = param.Unit,
                    Desc = param.Desc
                });
            }

            // 複製 Benchmarks
            foreach (var benchmark in data.Benchmarks)
            {
                record.Benchmarks.Add(new BenchmarkItem
                {
                    Code = benchmark.Code,
                    Name = benchmark.Name ?? benchmark.Code ?? string.Empty,
                    Value = benchmark.Value,
                    Unit = benchmark.Unit,
                    Desc = benchmark.Desc
                });
            }

            // 複製 OtherData
            foreach (var other in data.OtherData)
            {
                record.OtherData.Add(new OtherDataItem
                {
                    Code = other.Code,
                    Name = other.Name ?? other.Code ?? string.Empty,
                    Value = other.Value ?? string.Empty,
                    Unit = other.Unit,
                    Desc = other.Desc
                });
            }

            await queueService.EnqueueAsync(record, errorMessage, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Graceful shutdown initiated - completing channel items...");

        try
        {
            // Complete the channel (no more writes)
            _inspectionChannel.Writer.Complete();

            // Allow base class to trigger ExecuteAsync cancellation
            await base.StopAsync(cancellationToken);

            _logger.LogInformation("Graceful shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception during shutdown (this is expected during application exit)");
        }
    }

    /// <summary>
    /// 將發送到 MES Cloud 的資料記錄到檔案
    /// </summary>
    private void LogMesUploadToFile(LabViewInspectionRequest? request, bool success, string? errorMessage)
    {
        _logger.LogInformation("LogMesUploadToFile called - success: {Success}, request is null: {IsNull}",
            success, request == null);

        try
        {
            // Null 檢查
            if (request == null)
            {
                _logger.LogWarning("LogMesUploadToFile called with null request");
                return;
            }

            // 確保目錄存在
            _logger.LogInformation("Log directory: {Directory}", _logDirectory);
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
                _logger.LogInformation("Created MES upload log directory: {Directory}", _logDirectory);
            }

            // 產生以日期命名的檔案名稱
            var today = DateTime.Now;
            var filename = $"mes-upload-{today:yyyy-MM-dd}.txt";
            var filepath = Path.Combine(_logDirectory, filename);
            _logger.LogInformation("Writing to log file: {Filepath}", filepath);

            // 直接序列化原始請求為 JSON (加上額外的 try-catch)
            string jsonContent;
            try
            {
                jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                _logger.LogInformation("JSON serialized successfully, length: {Length}", jsonContent.Length);
            }
            catch (Exception serializeEx)
            {
                _logger.LogWarning(serializeEx, "Failed to serialize request to JSON");
                jsonContent = $"{{\"error\":\"Serialization failed: {serializeEx.Message}\"}}";
            }

            // 取得 TraceCode 或 LotNo 用於記錄
            var firstData = request.Data?.FirstOrDefault();
            var traceInfo = firstData?.TraceCode ?? firstData?.LotNo ?? "N/A";
            var status = success ? "SUCCESS" : "FAILED";
            var errorPart = string.IsNullOrEmpty(errorMessage) ? "" : $" | Error: {errorMessage}";

            // 組成記錄行 (含時間戳記、TraceCode、狀態)
            var logEntry = $"[{today:yyyy-MM-dd HH:mm:ss.fff}] [{status}] [TraceCode: {traceInfo}]{errorPart} {jsonContent}{Environment.NewLine}";

            // 執行緒安全寫入檔案，使用 FileStream 確保立即寫入磁碟
            lock (_logLock)
            {
                using (var fs = new FileStream(filepath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write(logEntry);
                    sw.Flush();
                }

                // 驗證檔案已寫入
                var fileInfo = new FileInfo(filepath);
                _logger.LogInformation("File written - exists: {Exists}, size: {Size} bytes",
                    fileInfo.Exists, fileInfo.Length);
            }

            _logger.LogInformation("MES upload data logged to file: {Filepath} (TraceCode: {TraceCode}, Status: {Status})",
                filepath, traceInfo, status);

            // 每小時執行一次清理檢查
            if ((DateTime.Now - _lastCleanup).TotalHours >= 1)
            {
                CleanupOldLogFiles();
                _lastCleanup = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log MES upload data to file: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 清理超過保留天數的舊記錄檔案
    /// </summary>
    private void CleanupOldLogFiles()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
                return;

            var cutoffDate = DateTime.Now.AddDays(-_retentionDays);
            var files = Directory.GetFiles(_logDirectory, "mes-upload-*.txt");
            var deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);

                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogInformation("Deleted old MES upload log file: {FileName} (LastWrite: {LastWrite})",
                            fileInfo.Name, fileInfo.LastWriteTime);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old log file: {File}", file);
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("MES upload log cleanup completed: deleted {Count} files older than {Days} days",
                    deletedCount, _retentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old MES upload log files");
        }
    }
}
