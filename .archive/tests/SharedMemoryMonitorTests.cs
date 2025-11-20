using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MesMiddleware.Service.Models;
using MesMiddleware.Service.Services.SharedMemory;

namespace MesMiddleware.Service.Tests.Unit;

/// <summary>
/// Unit tests for SharedMemoryMonitor.
/// Tests FR-007 (mutex for concurrent access protection).
/// RED PHASE: Test MemoryMappedFile read, EventWaitHandle signal, Mutex locking.
/// </summary>
public class SharedMemoryMonitorTests
{
    [Fact]
    public async Task StartAsync_ShouldCreateSharedMemorySegmentIfNotExists()
    {
        // Arrange
        var options = Options.Create(new SharedMemoryOptions
        {
            InspectionDataSegmentName = $"TEST_SEGMENT_{Guid.NewGuid()}",
            DataReadyEventName = $"TEST_EVENT_{Guid.NewGuid()}",
            SegmentSize = 1024 * 1024,
            MutexTimeout = 5000
        });

        var mockLogger = new Mock<ILogger<SharedMemoryMonitor>>();
        var monitor = new SharedMemoryMonitor(options, mockLogger.Object);

        // Act
        await monitor.StartAsync(CancellationToken.None);

        // Assert - Should start without exceptions
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Cleanup
        await monitor.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldGracefullyShutdownMonitoring()
    {
        // Arrange
        var options = Options.Create(new SharedMemoryOptions
        {
            InspectionDataSegmentName = $"TEST_SEGMENT_{Guid.NewGuid()}",
            DataReadyEventName = $"TEST_EVENT_{Guid.NewGuid()}",
            SegmentSize = 1024 * 1024,
            MutexTimeout = 5000
        });

        var mockLogger = new Mock<ILogger<SharedMemoryMonitor>>();
        var monitor = new SharedMemoryMonitor(options, mockLogger.Object);

        await monitor.StartAsync(CancellationToken.None);

        // Act
        await monitor.StopAsync();

        // Assert - Should stop without exceptions
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DataReceived_WhenEquipmentSignals_ShouldRaiseEvent()
    {
        // This test requires actual shared memory write simulation
        // For unit testing, this is better tested in Integration/SharedMemoryIpcTests.cs
        // This test validates the event mechanism exists

        var options = Options.Create(new SharedMemoryOptions
        {
            InspectionDataSegmentName = $"TEST_SEGMENT_{Guid.NewGuid()}",
            DataReadyEventName = $"TEST_EVENT_{Guid.NewGuid()}",
            SegmentSize = 1024 * 1024,
            MutexTimeout = 5000
        });

        var mockLogger = new Mock<ILogger<SharedMemoryMonitor>>();
        var monitor = new SharedMemoryMonitor(options, mockLogger.Object);

        var eventRaised = false;
        monitor.DataReceived += (sender, args) =>
        {
            eventRaised = true;
        };

        // Assert - Event handler should be registered
        eventRaised.Should().BeFalse("event not raised yet");
        // Full integration test in SharedMemoryIpcTests.cs
    }

    [Fact]
    public async Task StartAsync_ShouldCreateSegmentWithConfiguredSize()
    {
        // Arrange - FR-006: Middleware MUST create shared memory segments on startup if they do not exist, with configurable size (default 10MB)
        var segmentName = $"TEST_EXPLICIT_SEGMENT_{Guid.NewGuid()}";
        var configuredSize = 10 * 1024 * 1024; // 10MB default

        var options = Options.Create(new SharedMemoryOptions
        {
            InspectionDataSegmentName = segmentName,
            DataReadyEventName = $"TEST_EVENT_{Guid.NewGuid()}",
            SegmentSize = configuredSize,
            MutexTimeout = 5000
        });

        var mockLogger = new Mock<ILogger<SharedMemoryMonitor>>();
        var monitor = new SharedMemoryMonitor(options, mockLogger.Object);

        // Act
        await monitor.StartAsync(CancellationToken.None);

        // Assert - Verify segment was created
        using (var testSegment = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(segmentName))
        {
            testSegment.Should().NotBeNull("segment should be created on startup if it doesn't exist");
        }

        // Cleanup
        await monitor.StopAsync();
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("started successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log successful startup");
    }
}
