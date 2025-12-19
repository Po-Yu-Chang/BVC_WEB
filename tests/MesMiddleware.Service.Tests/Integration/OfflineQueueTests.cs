using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MesMiddleware.Service.Data;
using MesMiddleware.Service.Services.Queue;
using MesMiddleware.Service.Services.WebApi;
using MesMiddleware.Shared.Models;
using System.Text.Json;

namespace MesMiddleware.Service.Tests.Integration;

/// <summary>
/// Integration tests for offline queue functionality.
/// Tests FR-011 (queue failed uploads to SQLite), FR-012 (preserve FIFO order).
/// RED PHASE: Verify that failed uploads are queued and retried with exponential backoff.
/// </summary>
public class OfflineQueueTests : IDisposable
{
    private MiddlewareDbContext? _dbContext;
    private string _testDbPath;

    public OfflineQueueTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_queue_{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        _dbContext?.Database.EnsureDeleted();
        _dbContext?.Dispose();

        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }

        // Reset shared database name for next test
        _sharedDbName = null;
    }

    [Fact]
    public async Task QueueUpload_WhenWebApiUnavailable_ShouldPersistToDatabase()
    {
        // Arrange
        _dbContext = CreateInMemoryDbContext();
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        var mockHangfireClient = new Mock<IBackgroundJobClient>();
        var mockLogger = new Mock<ILogger<UploadQueueService>>();

        var queueService = new UploadQueueService(
            _dbContext,
            mockWebApiClient.Object,
            mockHangfireClient.Object,
            mockLogger.Object);

        var testData = CreateTestInspectionRecord();

        // Act - Queue upload due to WebAPI failure
        var queueId = await queueService.QueueUploadAsync(
            testData,
            "WebAPI unavailable: HTTP 503",
            CancellationToken.None);

        // Assert - Entry should be persisted to database
        queueId.Should().NotBeEmpty();

        var queuedEntry = await _dbContext.QueuedUploads.FindAsync(queueId);
        queuedEntry.Should().NotBeNull();
        queuedEntry!.Status.Should().Be("Pending");
        queuedEntry.RetryCount.Should().Be(0);
        queuedEntry.LastError.Should().Contain("503");
        queuedEntry.TraceCodeOrLotNo.Should().Be("TRACE_TEST");

        // Verify JSON serialization preserved data
        var deserializedData = JsonSerializer.Deserialize<InspectionRecord>(
            queuedEntry.InspectionDataJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        deserializedData.Should().NotBeNull();
        deserializedData!.TraceCode.Should().Be(testData.TraceCode);

        // Verify Hangfire job was scheduled for retry in 2 seconds
        mockHangfireClient.Verify(
            x => x.Create(
                It.IsAny<Job>(),
                It.Is<ScheduledState>(s => s.EnqueueAt > DateTime.UtcNow && s.EnqueueAt <= DateTime.UtcNow.AddSeconds(3))),
            Times.Once);
    }

    [Fact]
    public async Task RetryQueuedUpload_WhenWebApiRecovered_ShouldRemoveFromQueue()
    {
        // Arrange
        _dbContext = CreateInMemoryDbContext();
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        mockWebApiClient
            .Setup(x => x.UploadInspectionDataAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // WebAPI recovered

        var mockHangfireClient = new Mock<IBackgroundJobClient>();
        var mockLogger = new Mock<ILogger<UploadQueueService>>();

        var queueService = new UploadQueueService(
            _dbContext,
            mockWebApiClient.Object,
            mockHangfireClient.Object,
            mockLogger.Object);

        // Queue an upload first
        var testData = CreateTestInspectionRecord();
        var queueId = await queueService.QueueUploadAsync(testData, "Initial failure", CancellationToken.None);

        // Act - Retry the queued upload (WebAPI now available)
        var retrySucceeded = await queueService.RetryQueuedUploadAsync(queueId);

        // Assert - Retry should succeed and entry should be removed
        retrySucceeded.Should().BeTrue();

        var queuedEntry = await _dbContext.QueuedUploads.FindAsync(queueId);
        queuedEntry.Should().BeNull("successful uploads should be removed from queue");

        // Verify WebAPI was called
        mockWebApiClient.Verify(
            x => x.UploadInspectionDataAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryQueuedUpload_WithContinuedFailure_ShouldUseExponentialBackoff()
    {
        // Arrange
        _dbContext = CreateInMemoryDbContext();
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        mockWebApiClient
            .Setup(x => x.UploadInspectionDataAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Continue failing

        var mockHangfireClient = new Mock<IBackgroundJobClient>();
        var mockLogger = new Mock<ILogger<UploadQueueService>>();

        var queueService = new UploadQueueService(
            _dbContext,
            mockWebApiClient.Object,
            mockHangfireClient.Object,
            mockLogger.Object);

        var testData = CreateTestInspectionRecord();
        var queueId = await queueService.QueueUploadAsync(testData, "Initial failure", CancellationToken.None);

        // Verify initial state
        var initialEntry = await _dbContext.QueuedUploads.AsNoTracking().FirstOrDefaultAsync(q => q.Id == queueId);
        initialEntry!.RetryCount.Should().Be(0, "initial queue entry should have RetryCount=0");

        // Act - Retry 3 times
        var result1 = await queueService.RetryQueuedUploadAsync(queueId); // Retry #1 (delay 2s)
        var entry1 = await _dbContext.QueuedUploads.AsNoTracking().FirstOrDefaultAsync(q => q.Id == queueId);

        var result2 = await queueService.RetryQueuedUploadAsync(queueId); // Retry #2 (delay 4s)
        var entry2 = await _dbContext.QueuedUploads.AsNoTracking().FirstOrDefaultAsync(q => q.Id == queueId);

        var result3 = await queueService.RetryQueuedUploadAsync(queueId); // Retry #3 (delay 8s)
        var entry3 = await _dbContext.QueuedUploads.AsNoTracking().FirstOrDefaultAsync(q => q.Id == queueId);

        // Assert - Exponential backoff: 2s, 4s, 8s
        entry1.Should().NotBeNull();
        entry1!.RetryCount.Should().Be(1);
        entry1.Status.Should().Be("Retrying");
        entry1.NextRetryAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(2), TimeSpan.FromSeconds(1));

        entry2!.RetryCount.Should().Be(2);
        entry2.NextRetryAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(4), TimeSpan.FromSeconds(1));

        entry3!.RetryCount.Should().Be(3);
        entry3.NextRetryAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(8), TimeSpan.FromSeconds(1));

        // Verify Hangfire scheduled retries with correct delays
        mockHangfireClient.Verify(
            x => x.Create(
                It.IsAny<Job>(),
                It.IsAny<ScheduledState>()),
            Times.Exactly(4)); // Initial + 3 retries
    }

    [Fact]
    public async Task RetryQueuedUpload_AfterMaxRetries_ShouldMoveToDeadLetterQueue()
    {
        // Arrange
        _dbContext = CreateInMemoryDbContext();
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        mockWebApiClient
            .Setup(x => x.UploadInspectionDataAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Always fail

        var mockHangfireClient = new Mock<IBackgroundJobClient>();
        var mockLogger = new Mock<ILogger<UploadQueueService>>();

        var queueService = new UploadQueueService(
            _dbContext,
            mockWebApiClient.Object,
            mockHangfireClient.Object,
            mockLogger.Object);

        var testData = CreateTestInspectionRecord();
        var queueId = await queueService.QueueUploadAsync(testData, "Initial failure", CancellationToken.None);

        // Act - Retry 5 times (max retries)
        for (int i = 0; i < 5; i++)
        {
            await queueService.RetryQueuedUploadAsync(queueId);
        }

        var finalEntry = await _dbContext.QueuedUploads.FindAsync(queueId);

        // Assert - After 5 retries, should be in "Failed" state (dead letter queue)
        finalEntry.Should().NotBeNull();
        finalEntry!.RetryCount.Should().Be(5);
        finalEntry.Status.Should().Be("Failed", "max retries exceeded");
        finalEntry.NextRetryAt.Should().BeNull("no more retries scheduled");
    }

    [Fact]
    public async Task GetQueueDepth_WithMixedStatuses_ShouldCountOnlyPendingAndRetrying()
    {
        // Arrange
        _dbContext = CreateInMemoryDbContext();
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        var mockHangfireClient = new Mock<IBackgroundJobClient>();
        var mockLogger = new Mock<ILogger<UploadQueueService>>();

        var queueService = new UploadQueueService(
            _dbContext,
            mockWebApiClient.Object,
            mockHangfireClient.Object,
            mockLogger.Object);

        // Add 3 Pending, 2 Retrying, 1 Failed
        for (int i = 0; i < 3; i++)
        {
            await queueService.QueueUploadAsync(CreateTestInspectionRecord(), "Pending test", CancellationToken.None);
        }

        // Manually create Retrying and Failed entries
        _dbContext.QueuedUploads.Add(new Models.QueuedUpload
        {
            InspectionDataJson = "{}",
            QueuedAt = DateTime.UtcNow,
            RetryCount = 2,
            Status = "Retrying",
            TraceCodeOrLotNo = "RETRY1"
        });

        _dbContext.QueuedUploads.Add(new Models.QueuedUpload
        {
            InspectionDataJson = "{}",
            QueuedAt = DateTime.UtcNow,
            RetryCount = 2,
            Status = "Retrying",
            TraceCodeOrLotNo = "RETRY2"
        });

        _dbContext.QueuedUploads.Add(new Models.QueuedUpload
        {
            InspectionDataJson = "{}",
            QueuedAt = DateTime.UtcNow,
            RetryCount = 5,
            Status = "Failed",
            TraceCodeOrLotNo = "FAILED1"
        });

        await _dbContext.SaveChangesAsync();

        // Act - Get queue depth
        var depth = await queueService.GetQueueDepthAsync(CancellationToken.None);

        // Assert - Should only count Pending (3) + Retrying (2) = 5 (not Failed)
        depth.Should().Be(5, "queue depth should only include Pending and Retrying statuses");
    }

    [Fact]
    public async Task QueuedUploads_ShouldPreserveFIFOOrder()
    {
        // Arrange
        _dbContext = CreateInMemoryDbContext();
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        var mockHangfireClient = new Mock<IBackgroundJobClient>();
        var mockLogger = new Mock<ILogger<UploadQueueService>>();

        var queueService = new UploadQueueService(
            _dbContext,
            mockWebApiClient.Object,
            mockHangfireClient.Object,
            mockLogger.Object);

        // Act - Queue 5 uploads in order
        var queueIds = new List<Guid>();
        for (int i = 1; i <= 5; i++)
        {
            await Task.Delay(10); // Ensure different timestamps
            var data = CreateTestInspectionRecord();
            data.TraceCode = $"TRACE_{i}";
            var queueId = await queueService.QueueUploadAsync(data, $"Error {i}", CancellationToken.None);
            queueIds.Add(queueId);
        }

        // Get queued uploads (should be ordered by QueuedAt descending)
        var queuedUploads = await queueService.GetQueuedUploadsAsync(100, CancellationToken.None);

        // Assert - Should maintain FIFO order (oldest first when processing, newest first when displaying)
        queuedUploads.Should().HaveCount(5);

        // Display order: newest first (TRACE_5, TRACE_4, ..., TRACE_1)
        queuedUploads[0].TraceCodeOrLotNo.Should().Be("TRACE_5");
        queuedUploads[4].TraceCodeOrLotNo.Should().Be("TRACE_1");

        // Verify database order for processing (oldest should have earliest QueuedAt)
        var oldestEntry = await _dbContext.QueuedUploads.OrderBy(q => q.QueuedAt).FirstAsync();
        oldestEntry.TraceCodeOrLotNo.Should().Be("TRACE_1", "FIFO order: oldest entry should be processed first");
    }

    private string? _sharedDbName;

    private MiddlewareDbContext CreateInMemoryDbContext()
    {
        // Use a shared database name for the entire test to ensure all contexts see the same data
        if (_sharedDbName == null)
        {
            _sharedDbName = $"TestDb_{Guid.NewGuid()}";
        }

        var options = new DbContextOptionsBuilder<MiddlewareDbContext>()
            .UseInMemoryDatabase(databaseName: _sharedDbName)
            .Options;

        var context = new MiddlewareDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static InspectionRecord CreateTestInspectionRecord()
    {
        return new InspectionRecord
        {
            ProcName = "Queue Test Inspection",
            DevName = "TEST-MACHINE",
            UserName = "test_operator",
            WorkClass = "Day",
            TraceCode = "TRACE_TEST",
            ParamData = new List<ParamDataItem>
            {
                new() { Name = "Param1", Value = "50.0", Unit = "mm", Status = "Pass" }
            },
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>(),
            InspectionTime = DateTime.UtcNow
        };
    }
}
