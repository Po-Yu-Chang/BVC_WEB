using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MesMiddleware.Service.Data;
using MesMiddleware.Service.Models;
using MesMiddleware.Service.Services.WebApi;
using MesMiddleware.Shared.Models;
using System.Text.Json;

namespace MesMiddleware.Service.Services.Queue;

/// <summary>
/// Upload queue service implementation using EF Core + SQLite + Hangfire.
/// Persists failed uploads, schedules retries with exponential backoff.
/// </summary>
public class UploadQueueService : IUploadQueueService
{
    private readonly MiddlewareDbContext _dbContext;
    private readonly IMesWebApiClient _webApiClient;
    private readonly IBackgroundJobClient _hangfireClient;
    private readonly ILogger<UploadQueueService> _logger;

    public UploadQueueService(
        MiddlewareDbContext dbContext,
        IMesWebApiClient webApiClient,
        IBackgroundJobClient hangfireClient,
        ILogger<UploadQueueService> logger)
    {
        _dbContext = dbContext;
        _webApiClient = webApiClient;
        _hangfireClient = hangfireClient;
        _logger = logger;
    }

    public async Task<Guid> QueueUploadAsync(
        InspectionRecord data,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queueEntry = new QueuedUpload
            {
                InspectionDataJson = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                QueuedAt = DateTime.UtcNow,
                RetryCount = 0,
                NextRetryAt = DateTime.UtcNow.AddSeconds(2), // Initial retry in 2 seconds
                LastError = errorMessage,
                Status = "Pending",
                MachineNumber = data.DevName, // Store machine/device for tracking
                TraceCodeOrLotNo = data.TraceCode ?? data.LotNo
            };

            await _dbContext.QueuedUploads.AddAsync(queueEntry, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Queued upload for {TraceCodeOrLot} due to: {Error}",
                queueEntry.TraceCodeOrLotNo,
                errorMessage);

            // Schedule Hangfire background job for retry (delayed by 2 seconds)
            _hangfireClient.Schedule<IUploadQueueService>(
                service => service.RetryQueuedUploadAsync(queueEntry.Id),
                TimeSpan.FromSeconds(2));

            return queueEntry.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue upload for later retry");
            throw;
        }
    }

    public async Task<bool> RetryQueuedUploadAsync(Guid queueId)
    {
        try
        {
            var queueEntry = await _dbContext.QueuedUploads.FindAsync(queueId);

            if (queueEntry == null)
            {
                _logger.LogWarning("Queue entry {QueueId} not found (may have been processed)", queueId);
                return false;
            }

            _logger.LogInformation("Retrying queued upload {QueueId} (retry #{RetryCount})",
                queueId,
                queueEntry.RetryCount + 1);

            // Deserialize inspection data
            var data = JsonSerializer.Deserialize<InspectionRecord>(queueEntry.InspectionDataJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data == null)
            {
                _logger.LogError("Failed to deserialize queued upload {QueueId}", queueId);
                queueEntry.Status = "Failed";
                queueEntry.LastError = "Deserialization failed";
                await _dbContext.SaveChangesAsync();
                return false;
            }

            // Attempt upload to WebAPI
            bool uploadSucceeded = false;
            try
            {
                uploadSucceeded = await _webApiClient.UploadInspectionDataAsync(data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retry upload {QueueId} failed", queueId);
                queueEntry.LastError = ex.Message;
            }

            if (uploadSucceeded)
            {
                // Success - remove from queue
                _logger.LogInformation("Queued upload {QueueId} succeeded, removing from queue", queueId);
                _dbContext.QueuedUploads.Remove(queueEntry);
                await _dbContext.SaveChangesAsync();
                return true;
            }

            // Failed - update retry info and reschedule
            queueEntry.RetryCount++;
            queueEntry.LastAttemptAt = DateTime.UtcNow;

            // Exponential backoff: 2s, 4s, 8s, 16s, 32s (max 5 retries)
            if (queueEntry.RetryCount >= 5)
            {
                _logger.LogError("Queue entry {QueueId} exceeded max retries (5), moving to dead letter queue",
                    queueId);
                queueEntry.Status = "Failed";
                queueEntry.NextRetryAt = null;
            }
            else
            {
                var delaySeconds = Math.Pow(2, queueEntry.RetryCount); // 2^1=2, 2^2=4, 2^3=8, etc.
                queueEntry.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                queueEntry.Status = "Retrying";

                _logger.LogInformation("Rescheduling queue entry {QueueId} for retry in {Delay}s",
                    queueId,
                    delaySeconds);

                // Schedule next retry with Hangfire
                _hangfireClient.Schedule<IUploadQueueService>(
                    service => service.RetryQueuedUploadAsync(queueId),
                    TimeSpan.FromSeconds(delaySeconds));
            }

            await _dbContext.SaveChangesAsync();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing queued upload {QueueId}", queueId);
            return false;
        }
    }

    public async Task<int> GetQueueDepthAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueuedUploads
            .Where(q => q.Status == "Pending" || q.Status == "Retrying")
            .CountAsync(cancellationToken);
    }

    public async Task<List<QueuedUploadInfo>> GetQueuedUploadsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueuedUploads
            .OrderByDescending(q => q.QueuedAt)
            .Take(limit)
            .Select(q => new QueuedUploadInfo
            {
                Id = q.Id,
                TraceCodeOrLotNo = q.TraceCodeOrLotNo ?? "N/A",
                QueuedAt = q.QueuedAt,
                RetryCount = q.RetryCount,
                NextRetryAt = q.NextRetryAt,
                Status = q.Status,
                LastError = q.LastError
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UploadHistoryItem>> GetRecentHistoryAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QueuedUploads
            .OrderByDescending(q => q.QueuedAt)
            .Take(limit)
            .Select(q => new UploadHistoryItem
            {
                Id = (int)(q.Id.GetHashCode() & 0x7FFFFFFF), // Convert Guid to int for display
                TraceCode = q.TraceCodeOrLotNo ?? "N/A",
                RowNo = q.MachineNumber ?? "N/A",
                CreatedAt = q.QueuedAt,
                Status = q.Status,
                RetryCount = q.RetryCount,
                LastErrorMessage = q.LastError
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RetryUploadAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find queue entry by hashed ID (this is a simplified approach)
            var queueEntry = await _dbContext.QueuedUploads
                .FirstOrDefaultAsync(q => (q.Id.GetHashCode() & 0x7FFFFFFF) == id, cancellationToken);

            if (queueEntry == null)
            {
                _logger.LogWarning("Queue entry with display ID {Id} not found", id);
                return false;
            }

            // Trigger immediate retry via Hangfire
            _hangfireClient.Enqueue<IUploadQueueService>(
                service => service.RetryQueuedUploadAsync(queueEntry.Id));

            _logger.LogInformation("Manual retry triggered for queue entry {QueueId}", queueEntry.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger manual retry for ID {Id}", id);
            return false;
        }
    }
}
