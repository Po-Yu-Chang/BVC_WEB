using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Service.Data;
using System.IO;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// Client for monitoring the MES Middleware Service via shared SQLite database.
/// Reads service status, upload history, and queue information from the middleware's database.
/// </summary>
public class MiddlewareApiClient : IMiddlewareApiClient
{
    private readonly string _databasePath;
    private readonly ILogger<MiddlewareApiClient> _logger;

    public MiddlewareApiClient(string databasePath, ILogger<MiddlewareApiClient> logger)
    {
        _databasePath = databasePath;
        _logger = logger;
    }

    private MiddlewareDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<MiddlewareDbContext>();
        optionsBuilder.UseSqlite($"Data Source={_databasePath}");
        return new MiddlewareDbContext(optionsBuilder.Options);
    }

    public async Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = CreateDbContext();

            var totalQueued = await dbContext.QueuedUploads
                .Where(q => q.Status == "Pending" || q.Status == "Retrying")
                .CountAsync(cancellationToken);

            var isDatabaseAccessible = File.Exists(_databasePath);

            return new ConnectionStatusDto
            {
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

    public async Task<List<UploadRecord>> GetUploadHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = CreateDbContext();

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

    public async Task<List<SharedMemoryActivity>> GetSharedMemoryActivityAsync(int maxRecords = 100, CancellationToken cancellationToken = default)
    {
        // TODO: Implement shared memory activity tracking
        // For now, return empty list. This would require adding an activity log table to the database.
        await Task.CompletedTask;
        return new List<SharedMemoryActivity>();
    }

    public async Task<List<CommandRecord>> GetCommandHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Implement command history tracking in database
            // For now, return empty list. This would require adding a CommandHistory table.
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
