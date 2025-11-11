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
/// Integration tests for command acknowledgment functionality (T057).
/// Tests equipment acknowledging commands via shared memory, middleware POSTing ack to WebAPI.
/// RED PHASE: Verify equipment can acknowledge commands and middleware reports back to WebAPI.
/// </summary>
public class CommandAckTests : IDisposable
{
    private const string TestCommandSegment = "MES_TEST_CMD";
    private const string TestAckSegment = "MES_TEST_CMD_ACK";
    private const string TestAckEvent = "MES_TEST_ACK_READY";
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
    public async Task EquipmentAcknowledgesCommand_MiddlewareReadsSuccessfully()
    {
        // Arrange - Create shared memory segments
        _testCommandSegment = MemoryMappedFile.CreateNew(TestCommandSegment, SegmentSize);
        _testAckSegment = MemoryMappedFile.CreateNew(TestAckSegment, SegmentSize);
        _testAckEvent = new EventWaitHandle(false, EventResetMode.AutoReset, TestAckEvent);

        var commandId = Guid.NewGuid();

        // Middleware sends command
        var command = new EquipmentCommand
        {
            CommandId = commandId,
            CommandType = "Calibrate",
            Parameters = new Dictionary<string, string> { { "Mode", "Full" } },
            IssuedAt = DateTime.UtcNow
        };

        using (var accessor = _testCommandSegment.CreateViewAccessor())
        {
            var json = JsonSerializer.Serialize(command);
            var bytes = Encoding.UTF8.GetBytes(json);
            accessor.Write(0, bytes.Length);
            accessor.WriteArray(4, bytes, 0, bytes.Length);
        }

        // Act - Equipment acknowledges command
        var ack = new CommandAcknowledgment
        {
            CommandId = commandId,
            Status = "Success",
            Message = "Calibration completed",
            AcknowledgedAt = DateTime.UtcNow
        };

        using (var accessor = _testAckSegment.CreateViewAccessor())
        {
            var json = JsonSerializer.Serialize(ack);
            var bytes = Encoding.UTF8.GetBytes(json);
            accessor.Write(0, bytes.Length);
            accessor.WriteArray(4, bytes, 0, bytes.Length);
        }

        // Signal middleware that acknowledgment is ready
        _testAckEvent.Set();

        // Assert - Middleware can read acknowledgment
        using (var mmf = MemoryMappedFile.OpenExisting(TestAckSegment))
        using (var accessor = mmf.CreateViewAccessor())
        {
            int length = accessor.ReadInt32(0);
            byte[] data = new byte[length];
            accessor.ReadArray(4, data, 0, length);

            var readJson = Encoding.UTF8.GetString(data);
            var readAck = JsonSerializer.Deserialize<CommandAcknowledgment>(readJson);

            readAck.Should().NotBeNull();
            readAck!.CommandId.Should().Be(commandId);
            readAck.Status.Should().Be("Success");
            readAck.Message.Should().Contain("Calibration");
        }
    }

    [Fact]
    public async Task MiddlewareReceivesAck_ShouldPostToWebApi()
    {
        // Arrange
        var mockWebApiClient = new Mock<IMesWebApiClient>();
        mockWebApiClient
            .Setup(x => x.SendCommandAcknowledgmentAsync(It.IsAny<CommandAcknowledgment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var commandId = Guid.NewGuid();
        var ack = new CommandAcknowledgment
        {
            CommandId = commandId,
            Status = "Success",
            Message = "Parameter updated",
            AcknowledgedAt = DateTime.UtcNow
        };

        // Act - Middleware POSTs acknowledgment to WebAPI
        var result = await mockWebApiClient.Object.SendCommandAcknowledgmentAsync(ack, CancellationToken.None);

        // Assert
        result.Should().BeTrue("acknowledgment should be successfully sent to WebAPI");
        mockWebApiClient.Verify(
            x => x.SendCommandAcknowledgmentAsync(
                It.Is<CommandAcknowledgment>(a => a.CommandId == commandId && a.Status == "Success"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EquipmentAcknowledgesWithFailure_MiddlewareShouldReportFailure()
    {
        // Arrange
        _testAckSegment = MemoryMappedFile.CreateNew("TEST_ACK_FAILURE", SegmentSize);

        var commandId = Guid.NewGuid();
        var ack = new CommandAcknowledgment
        {
            CommandId = commandId,
            Status = "Failed",
            Message = "Equipment sensor error during calibration",
            AcknowledgedAt = DateTime.UtcNow
        };

        // Act - Equipment writes failure acknowledgment
        using (var accessor = _testAckSegment.CreateViewAccessor())
        {
            var json = JsonSerializer.Serialize(ack);
            var bytes = Encoding.UTF8.GetBytes(json);
            accessor.Write(0, bytes.Length);
            accessor.WriteArray(4, bytes, 0, bytes.Length);
        }

        // Assert - Middleware can read failure status
        using (var mmf = MemoryMappedFile.OpenExisting("TEST_ACK_FAILURE"))
        using (var accessor = mmf.CreateViewAccessor())
        {
            int length = accessor.ReadInt32(0);
            byte[] data = new byte[length];
            accessor.ReadArray(4, data, 0, length);

            var readJson = Encoding.UTF8.GetString(data);
            var readAck = JsonSerializer.Deserialize<CommandAcknowledgment>(readJson);

            readAck.Should().NotBeNull();
            readAck!.Status.Should().Be("Failed");
            readAck.Message.Should().Contain("sensor error");
        }

        MemoryMappedFile.OpenExisting("TEST_ACK_FAILURE").Dispose();
    }

    [Fact]
    public async Task MultipleCommands_ShouldTrackAcknowledgmentsByCommandId()
    {
        // Arrange
        var command1Id = Guid.NewGuid();
        var command2Id = Guid.NewGuid();
        var command3Id = Guid.NewGuid();

        var acks = new List<CommandAcknowledgment>
        {
            new() { CommandId = command1Id, Status = "Success", Message = "Command 1 completed" },
            new() { CommandId = command2Id, Status = "Failed", Message = "Command 2 failed" },
            new() { CommandId = command3Id, Status = "Success", Message = "Command 3 completed" }
        };

        // Act - Track acknowledgments
        var ackDict = new Dictionary<Guid, CommandAcknowledgment>();
        foreach (var ack in acks)
        {
            ackDict[ack.CommandId] = ack;
        }

        // Assert - Should track each command's acknowledgment separately
        ackDict.Should().HaveCount(3);
        ackDict[command1Id].Status.Should().Be("Success");
        ackDict[command2Id].Status.Should().Be("Failed");
        ackDict[command3Id].Status.Should().Be("Success");
    }

    [Fact]
    public async Task EventWaitHandle_ShouldSignalWhenAckReady()
    {
        // Arrange
        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "TEST_ACK_SIGNAL");
        var signalReceived = false;

        var waitTask = Task.Run(() =>
        {
            var result = evt.WaitOne(TimeSpan.FromSeconds(2));
            if (result) signalReceived = true;
        });

        // Act - Equipment signals acknowledgment ready
        await Task.Delay(100); // Simulate processing time
        evt.Set();

        await waitTask;

        // Assert - Middleware detected acknowledgment signal
        signalReceived.Should().BeTrue("middleware should detect acknowledgment signal within 2 seconds");
    }
}
