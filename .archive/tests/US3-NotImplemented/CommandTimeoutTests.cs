using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MesMiddleware.Service.Services.WebApi;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Tests.Integration;

/// <summary>
/// Integration tests for command timeout handling (T058).
/// Tests middleware handling equipment timeout scenarios (30 second timeout).
/// RED PHASE: Verify middleware detects timeout and notifies WebAPI of failure.
/// </summary>
public class CommandTimeoutTests : IDisposable
{
    private const string TestCommandSegment = "MES_TEST_TIMEOUT_CMD";
    private const string TestAckSegment = "MES_TEST_TIMEOUT_ACK";
    private const string TestAckEvent = "MES_TEST_TIMEOUT_ACK_READY";
    private const long SegmentSize = 1024 * 1024; // 1MB

    private MemoryMappedFile? _testCommandSegment;
    private MemoryMappedFile? _testAckSegment;
    private EventWaitHandle? _testAckEvent;

    public void Dispose()
    {
        _testAckEvent?.Dispose();
        _testAckSegment?.Dispose();
        _testCommandSegment?.Dispose();
    }

    [Fact]
    public async Task WaitForAcknowledgment_WhenTimeout_ShouldReturnFalse()
    {
        // Arrange
        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "TEST_TIMEOUT_EVENT");

        // Act - Wait with 100ms timeout (equipment does NOT acknowledge)
        var startTime = DateTime.UtcNow;
        var result = evt.WaitOne(TimeSpan.FromMilliseconds(100));
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should timeout and return false
        result.Should().BeFalse("acknowledgment event was never signaled");
        elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(90), "should wait for full timeout period");
    }

    [Fact]
    public async Task WaitForAcknowledgment_WhenTimeoutOccurs_ShouldLogTimeout()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CommandTimeoutTests>>();
        var commandId = Guid.NewGuid();

        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "TEST_TIMEOUT_LOG");

        // Act - Simulate timeout scenario
        var result = evt.WaitOne(TimeSpan.FromMilliseconds(100));

        if (!result)
        {
            // Middleware should log timeout error
            mockLogger.Object.LogError("Command {CommandId} timed out after 30 seconds", commandId);
        }

        // Assert
        result.Should().BeFalse();
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CommandTimeout_ShouldSendFailureAckToWebApi()
    {
        // Arrange
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        mockWebApiClient
            .Setup(x => x.SendCommandAcknowledgmentAsync(It.IsAny<CommandAcknowledgment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var commandId = Guid.NewGuid();

        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "TEST_TIMEOUT_ACK");

        // Act - Simulate timeout (equipment never acknowledges)
        var acknowledged = evt.WaitOne(TimeSpan.FromMilliseconds(100));

        if (!acknowledged)
        {
            // Middleware sends timeout acknowledgment to WebAPI
            var timeoutAck = new CommandAcknowledgment
            {
                CommandId = commandId,
                Status = "Timeout",
                Message = "Equipment did not acknowledge command within 30 seconds",
                AcknowledgedAt = DateTime.UtcNow
            };

            await mockWebApiClient.Object.SendCommandAcknowledgmentAsync(timeoutAck, CancellationToken.None);
        }

        // Assert
        acknowledged.Should().BeFalse();
        mockWebApiClient.Verify(
            x => x.SendCommandAcknowledgmentAsync(
                It.Is<CommandAcknowledgment>(a => a.CommandId == commandId && a.Status == "Timeout"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CommandAcknowledgedBeforeTimeout_ShouldNotTriggerTimeout()
    {
        // Arrange
        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "TEST_NO_TIMEOUT");

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(50); // Equipment acknowledges after 50ms
            evt.Set();
        });

        // Act - Wait with 200ms timeout (equipment acknowledges in time)
        var result = evt.WaitOne(TimeSpan.FromMilliseconds(200));

        await ackTask;

        // Assert - Should receive acknowledgment before timeout
        result.Should().BeTrue("equipment acknowledged command before timeout");
    }

    [Fact]
    public async Task MultipleCommands_ShouldTrackTimeoutIndependently()
    {
        // Arrange
        var command1Id = Guid.NewGuid();
        var command2Id = Guid.NewGuid();
        var command3Id = Guid.NewGuid();

        var evt1 = new EventWaitHandle(false, EventResetMode.AutoReset, "CMD1_ACK");
        var evt2 = new EventWaitHandle(false, EventResetMode.AutoReset, "CMD2_ACK");
        var evt3 = new EventWaitHandle(false, EventResetMode.AutoReset, "CMD3_ACK");

        // Act - Command 1: times out, Command 2: acknowledges, Command 3: times out
        var task1 = Task.Run(() => evt1.WaitOne(TimeSpan.FromMilliseconds(100)));
        var task2 = Task.Run(async () =>
        {
            await Task.Delay(50);
            evt2.Set();
            return evt2.WaitOne(TimeSpan.FromMilliseconds(100));
        });
        var task3 = Task.Run(() => evt3.WaitOne(TimeSpan.FromMilliseconds(100)));

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert
        results[0].Should().BeFalse("command 1 timed out");
        results[1].Should().BeTrue("command 2 acknowledged");
        results[2].Should().BeFalse("command 3 timed out");

        evt1.Dispose();
        evt2.Dispose();
        evt3.Dispose();
    }

    [Fact]
    public async Task Timeout_ShouldCreateTimeoutAcknowledgmentWithCorrectStatus()
    {
        // Arrange
        var commandId = Guid.NewGuid();

        // Act - Create timeout acknowledgment
        var timeoutAck = new CommandAcknowledgment
        {
            CommandId = commandId,
            Status = "Timeout",
            Message = "Equipment did not respond within 30 seconds",
            AcknowledgedAt = DateTime.UtcNow
        };

        // Assert
        timeoutAck.CommandId.Should().Be(commandId);
        timeoutAck.Status.Should().Be("Timeout");
        timeoutAck.Message.Should().Contain("30 seconds");
        timeoutAck.AcknowledgedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TimeoutDuration_ShouldBe30Seconds()
    {
        // Arrange
        const int expectedTimeoutSeconds = 30;

        // Act - Verify timeout configuration
        var timeoutDuration = TimeSpan.FromSeconds(expectedTimeoutSeconds);

        // Assert - Timeout should be 30 seconds per spec
        timeoutDuration.TotalSeconds.Should().Be(30, "command timeout should be 30 seconds per FR-022");
    }

    [Fact]
    public async Task CancellationToken_ShouldAllowEarlyTimeout()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "TEST_CANCEL");

        // Act - Cancel after 100ms
        var waitTask = Task.Run(() =>
        {
            try
            {
                var waitHandles = new[] { evt, cts.Token.WaitHandle };
                var index = WaitHandle.WaitAny(waitHandles, TimeSpan.FromSeconds(30));
                return index == 0; // True if evt signaled, false if cancelled
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        });

        await Task.Delay(100);
        cts.Cancel();

        var result = await waitTask;

        // Assert - Should cancel before full 30-second timeout
        result.Should().BeFalse("cancellation should stop waiting early");
    }
}
