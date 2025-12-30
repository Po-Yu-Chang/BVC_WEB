using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MesMiddleware.Shared.Models.LabView;
using System.IO;
using System.Text.Json;
using System.Threading.Channels;

namespace MesMiddleware.Monitor.Controllers;

/// <summary>
/// API endpoint for LabVIEW equipment to submit inspection data.
/// Accepts LabVIEW JSON format and forwards directly to MES Cloud (no conversion).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LabViewController : ControllerBase
{
    private readonly Channel<LabViewInspectionRequest> _inspectionChannel;
    private readonly ILogger<LabViewController> _logger;

    // LabVIEW 資料記錄相關
    private static readonly object _logLock = new object();
    private static readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "labview-logs");
    private static DateTime _lastCleanup = DateTime.MinValue;
    private static readonly int _retentionDays = 7;

    public LabViewController(
        Channel<LabViewInspectionRequest> inspectionChannel,
        ILogger<LabViewController> logger)
    {
        _inspectionChannel = inspectionChannel;
        _logger = logger;
    }

    /// <summary>
    /// Submit inspection data from LabVIEW equipment.
    /// Endpoint: POST /api/labview/submit
    /// Accepts: {"isVerifyLot": false, "data": [...]}
    /// </summary>
    /// <param name="request">LabVIEW inspection request JSON</param>
    /// <returns>202 Accepted if data queued successfully</returns>
    [HttpPost("submit")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitLabViewData([FromBody] LabViewInspectionRequest request)
    {
        try
        {
            // 記錄 LabVIEW 傳送的原始資料
            LogLabViewDataToFile(request);

            // Validate request structure
            if (request.Data == null || request.Data.Count == 0)
            {
                _logger.LogWarning("LabVIEW request rejected: no data provided");
                return BadRequest(new
                {
                    success = false,
                    code = "400",
                    msg = "No inspection data provided in request",
                    data = ""
                });
            }

            // Validate required fields for each data item
            var errors = ValidateLabViewData(request);
            if (errors.Any())
            {
                _logger.LogWarning("LabVIEW validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new
                {
                    success = false,
                    code = "400",
                    msg = string.Join("; ", errors),
                    data = ""
                });
            }

            // 直接將原始請求寫入 Channel (不做轉換，避免資料遺失)
            await _inspectionChannel.Writer.WriteAsync(request);

            // Record to Device -> Monitor history (使用第一筆資料的 TraceCode)
            var firstData = request.Data.First();
            var traceInfo = firstData.TraceCode ?? firstData.LotNo ?? "N/A";

            StatusController.AddDeviceToMonitorHistory(traceInfo, "Received", null);

            _logger.LogInformation("LabVIEW data accepted: {TraceCodeOrLot} (DevName: {DevName}, Count: {Count})",
                traceInfo,
                firstData.DevName,
                request.Data.Count);

            // Update device activity status
            StatusController.UpdateDeviceActivity();

            // Return MES-compatible success response
            return Accepted(new
            {
                success = true,
                code = "200",
                msg = $"Accepted {request.Data.Count} records",
                data = ""
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process LabVIEW inspection data");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                code = "500",
                msg = "Internal server error: " + ex.Message,
                data = ""
            });
        }
    }

    /// <summary>
    /// 將 LabVIEW 傳送的資料記錄到檔案
    /// 檔案命名格式: labview-YYYY-MM-DD.txt
    /// </summary>
    private void LogLabViewDataToFile(LabViewInspectionRequest? request)
    {
        try
        {
            // Null 檢查
            if (request == null)
            {
                _logger.LogWarning("LogLabViewDataToFile called with null request");
                return;
            }

            // 確保目錄存在
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
                _logger.LogInformation("Created LabVIEW log directory: {Directory}", _logDirectory);
            }

            // 產生以日期命名的檔案名稱
            var today = DateTime.Now;
            var filename = $"labview-{today:yyyy-MM-dd}.txt";
            var filepath = Path.Combine(_logDirectory, filename);

            // 序列化請求資料為 JSON (加上額外的 try-catch)
            string jsonContent;
            try
            {
                jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch (Exception serializeEx)
            {
                _logger.LogWarning(serializeEx, "Failed to serialize LabVIEW request to JSON");
                jsonContent = $"{{\"error\":\"Serialization failed: {serializeEx.Message}\"}}";
            }

            // 取得 TraceCode 或 LotNo 用於記錄
            var traceInfo = request.Data?.FirstOrDefault()?.TraceCode
                ?? request.Data?.FirstOrDefault()?.LotNo
                ?? "N/A";

            // 組成記錄行 (含時間戳記和 TraceCode)
            var logEntry = $"[{today:yyyy-MM-dd HH:mm:ss.fff}] [TraceCode: {traceInfo}] {jsonContent}{Environment.NewLine}";

            // 執行緒安全寫入檔案，使用 FileStream 確保立即寫入磁碟
            lock (_logLock)
            {
                using (var fs = new FileStream(filepath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write(logEntry);
                    sw.Flush();
                }
            }

            _logger.LogInformation("LabVIEW data logged to file: {Filepath} (TraceCode: {TraceCode})", filepath, traceInfo);

            // 每小時執行一次清理檢查（避免每次請求都檢查）
            if ((DateTime.Now - _lastCleanup).TotalHours >= 1)
            {
                CleanupOldLogFiles();
                _lastCleanup = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            // 記錄失敗不應影響主要功能，只記錄警告（包含完整例外訊息）
            _logger.LogWarning(ex, "Failed to log LabVIEW data to file: {Message}", ex.Message);
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
            var files = Directory.GetFiles(_logDirectory, "labview-*.txt");
            var deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);

                    // 根據檔案最後寫入時間判斷是否過期
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        System.IO.File.Delete(file);
                        deletedCount++;
                        _logger.LogInformation("Deleted old LabVIEW log file: {FileName} (LastWrite: {LastWrite})",
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
                _logger.LogInformation("LabVIEW log cleanup completed: deleted {Count} files older than {Days} days",
                    deletedCount, _retentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old LabVIEW log files");
        }
    }

    /// <summary>
    /// Validate LabVIEW inspection data
    /// </summary>
    private List<string> ValidateLabViewData(LabViewInspectionRequest request)
    {
        var errors = new List<string>();

        for (int i = 0; i < request.Data.Count; i++)
        {
            var data = request.Data[i];
            var prefix = request.Data.Count > 1 ? $"[{i}] " : "";

            if (string.IsNullOrWhiteSpace(data.ProcName))
                errors.Add($"{prefix}procName is required");

            if (string.IsNullOrWhiteSpace(data.DevName))
                errors.Add($"{prefix}devName is required");

            if (string.IsNullOrWhiteSpace(data.UserName))
                errors.Add($"{prefix}userName is required");

            if (string.IsNullOrWhiteSpace(data.WorkClass))
                errors.Add($"{prefix}workClass is required");

            if (string.IsNullOrWhiteSpace(data.TraceCode) && string.IsNullOrWhiteSpace(data.LotNo))
                errors.Add($"{prefix}Either traceCode or lotNo must be provided");

            // Validate paramData items have values
            foreach (var param in data.ParamData)
            {
                if (string.IsNullOrWhiteSpace(param.Code) && string.IsNullOrWhiteSpace(param.Name))
                    errors.Add($"{prefix}paramData item missing code/name");
            }

            // Check for CheckTime in otherData
            var hasCheckTime = data.OtherData.Any(x =>
                x.Code?.Equals("CheckTime", StringComparison.OrdinalIgnoreCase) == true ||
                x.Name?.Equals("CheckTime", StringComparison.OrdinalIgnoreCase) == true);

            if (!hasCheckTime)
            {
                _logger.LogWarning("{Prefix}otherData missing CheckTime, will use current time", prefix);
            }
        }

        return errors;
    }
}
