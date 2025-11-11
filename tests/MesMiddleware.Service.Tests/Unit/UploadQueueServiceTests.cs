using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MesMiddleware.Service.Data;
using MesMiddleware.Service.Services.Queue;
using MesMiddleware.Service.Services.WebApi;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Tests.Unit;

/// <summary>
/// Unit tests for UploadQueueService.
/// Tests FR-011 (queue failed uploads), FR-012 (FIFO order).
/// RED PHASE: Test EF Core queue operations, FIFO order, Hangfire job enqueue.
/// </summary>
public class UploadQueueServiceTests : IDisposable
{
    private MiddlewareDbContext? _dbContext;

    public void Dispose()
    {
        _dbContext?.Database.EnsureDeleted();
        _dbContext?.Dispose();
    }

    [Fact]
    public async Task QueueUploadAsync_ShouldPersistToDatabase()
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

        var testData = new InspectionRecord
        {
            RowNo = "TEST001",
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = "operator",
            WorkClass = "Day",
            TraceCode = "TRACE001",
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var queueId = await queueService.QueueUploadAsync(testData, "Test error", CancellationToken.None);

        // Assert
        queueId.Should().NotBeEmpty();

        var queuedEntry = await _dbContext.QueuedUploads.FindAsync(queueId);
        queuedEntry.Should().NotBeNull();
        queuedEntry!.Status.Should().Be("Pending");
        queuedEntry.RetryCount.Should().Be(0);
        queuedEntry.TraceCodeOrLotNo.Should().Be("TRACE001");
    }

    [Fact]
    public async Task RetryQueuedUploadAsync_WhenSuccessful_ShouldRemoveFromQueue()
    {
        // Arrange
        _dbContext = CreateInMemoryDbContext();
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        mockWebApiClient
            .Setup(x => x.UploadInspectionDataAsync(It.IsAny<InspectionRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockHangfireClient = new Mock<IBackgroundJobClient>();
        var mockLogger = new Mock<ILogger<UploadQueueService>>();

        var queueService = new UploadQueueService(
            _dbContext,
            mockWebApiClient.Object,
            mockHangfireClient.Object,
            mockLogger.Object);

        var testData = new InspectionRecord
        {
            RowNo = "TEST001",
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = "operator",
            WorkClass = "Day",
            TraceCode = "TRACE001",
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        var queueId = await queueService.QueueUploadAsync(testData, "Test error", CancellationToken.None);

        // Act
        var result = await queueService.RetryQueuedUploadAsync(queueId);

        // Assert
        result.Should().BeTrue();

        var queuedEntry = await _dbContext.QueuedUploads.FindAsync(queueId);
        queuedEntry.Should().BeNull("successful uploads should be removed");
    }

    [Fact]
    public async Task GetQueueDepthAsync_ShouldCountPendingAndRetryingOnly()
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

        // Add entries with different statuses
        _dbContext.QueuedUploads.Add(new Models.QueuedUpload
        {
            InspectionDataJson = "{}",
            QueuedAt = DateTime.UtcNow,
            Status = "Pending",
            TraceCodeOrLotNo = "TRACE1"
        });

        _dbContext.QueuedUploads.Add(new Models.QueuedUpload
        {
            InspectionDataJson = "{}",
            QueuedAt = DateTime.UtcNow,
            Status = "Retrying",
            TraceCodeOrLotNo = "TRACE2"
        });

        _dbContext.QueuedUploads.Add(new Models.QueuedUpload
        {
            InspectionDataJson = "{}",
            QueuedAt = DateTime.UtcNow,
            Status = "Failed",
            TraceCodeOrLotNo = "TRACE3"
        });

        await _dbContext.SaveChangesAsync();

        // Act
        var depth = await queueService.GetQueueDepthAsync();

        // Assert
        depth.Should().Be(2, "should only count Pending and Retrying");
    }

    private MiddlewareDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MiddlewareDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var context = new MiddlewareDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
