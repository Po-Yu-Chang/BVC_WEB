using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Service.Data;
using System.IO;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 透過共享 SQLite 資料庫監控 MES 中介軟體服務的客戶端
/// 從中介軟體的資料庫讀取服務狀態、上傳歷史和佇列資訊
/// </summary>
public class MiddlewareApiClient : IMiddlewareApiClient
{
    private readonly string _databasePath;
    private readonly ILogger<MiddlewareApiClient> _logger;

    /// <summary>
    /// 建構函式，初始化中介軟體 API 客戶端
    /// </summary>
    /// <param name="databasePath">資料庫檔案路徑</param>
    /// <param name="logger">日誌記錄器</param>
    public MiddlewareApiClient(string databasePath, ILogger<MiddlewareApiClient> logger)
    {
        _databasePath = databasePath;
        _logger = logger;
    }

    /// <summary>
    /// 建立資料庫上下文
    /// </summary>
    /// <returns>中介軟體資料庫上下文</returns>
    private MiddlewareDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<MiddlewareDbContext>();
        optionsBuilder.UseSqlite($"Data Source={_databasePath}");
        return new MiddlewareDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// 取得連線狀態
    /// </summary>
    public async Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = CreateDbContext();

            // 計算等待中或重試中的項目總數
            var totalQueued = await dbContext.QueuedUploads
                .Where(q => q.Status == "Pending" || q.Status == "Retrying")
                .CountAsync(cancellationToken);

            // 檢查資料庫是否可存取
            var isDatabaseAccessible = File.Exists(_databasePath);

            return new ConnectionStatusDto
            {
                // 根據資料庫可存取性和佇列狀態決定連線狀態
                Status = isDatabaseAccessible ? (totalQueued == 0 ? "Connected" : "Retrying") : "Disconnected",
                LastPingTimestamp = DateTime.UtcNow,
                QueueDepth = totalQueued
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection status");
            return new ConnectionStatusDto
            {
                Status = "Error",
                LastPingTimestamp = DateTime.UtcNow,
                QueueDepth = 0
            };
        }
    }

    /// <summary>
    /// 取得上傳歷史記錄
    /// </summary>
    public async Task<List<UploadRecord>> GetUploadHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = CreateDbContext();

            // 從資料庫取得最新的上傳記錄
            var uploadRecords = await dbContext.QueuedUploads
                .OrderByDescending(q => q.QueuedAt)
                .Take(maxRecords)
                .Select(q => new UploadRecord
                {
                    Id = q.Id,
                    Timestamp = q.QueuedAt,
                    TraceCode = q.TraceCodeOrLotNo ?? "N/A",
                    EquipmentName = q.MachineNumber ?? "Unknown",
                    Status = q.Status,
                    ErrorMessage = q.LastError
                })
                .ToListAsync(cancellationToken);

            return uploadRecords;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get upload history from database");
            return new List<UploadRecord>();
        }
    }

    /// <summary>
    /// 取得共享記憶體活動記錄
    /// </summary>
    public async Task<List<SharedMemoryActivity>> GetSharedMemoryActivityAsync(int maxRecords = 100, CancellationToken cancellationToken = default)
    {
        // TODO: 實作共享記憶體活動追蹤
        // 目前返回空清單。需要在資料庫中新增活動日誌表。
        await Task.CompletedTask;
        return new List<SharedMemoryActivity>();
    }

    /// <summary>
    /// 取得命令歷史記錄
    /// </summary>
    public async Task<List<CommandRecord>> GetCommandHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: 實作資料庫中的命令歷史追蹤
            // 目前返回空清單。需要新增 CommandHistory 表。
            _logger.LogDebug("Getting command history (max {MaxRecords} records)", maxRecords);

            await Task.CompletedTask;
            return new List<CommandRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get command history from database");
            return new List<CommandRecord>();
        }
    }
}
