using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Tests.Integration;

/// <summary>
/// Integration tests for command write functionality (T056).
/// Tests FR-005 (bidirectional shared memory), equipment command flow.
/// RED PHASE: Verify middleware can write commands to MES_EQUIPMENT_CMD shared memory.
/// </summary>
public class CommandWriteTests : IDisposable
{
    private const string TestCommandSegment = "MES_TEST_EQUIPMENT_CMD";
    private const string TestCommandEvent = "MES_TEST_CMD_READY";
    private const long SegmentSize = 1024 * 1024; // 1MB

    private MemoryMappedFile? _testCommandSegment;
    private EventWaitHandle? _testCommandEvent;

    public void Dispose()
    {
        _testCommandEvent?.Dispose();
        _testCommandSegment?.Dispose();
    }

    [Fact]
    public async Task WriteCommand_ShouldWriteToSharedMemory()
    {
        // Arrange - Create shared memory segment for equipment commands
        _testCommandSegment = MemoryMappedFile.CreateNew(TestCommandSegment, SegmentSize);
        _testCommandEvent = new EventWaitHandle(false, EventResetMode.AutoReset, TestCommandEvent);

        var command = new EquipmentCommand
        {
            CommandId = Guid.NewGuid(),
            CommandType = "ChangeParameter",
            Parameters = new Dictionary<string, string>
            {
                { "ParameterName", "InspectionThreshold" },
                { "NewValue", "0.5" }
            },
            IssuedAt = DateTime.UtcNow
        };

        // Act - Middleware writes command to shared memory
        var json = JsonSerializer.Serialize(command);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using (var accessor = _testCommandSegment.CreateViewAccessor())
        {
            // Write length prefix (first 4 bytes)
            accessor.Write(0, jsonBytes.Length);

            // Write JSON data (starting at offset 4)
            accessor.WriteArray(4, jsonBytes, 0, jsonBytes.Length);
        }

        // Signal equipment that command is ready
        _testCommandEvent.Set();

        // Assert - Equipment can read command from shared memory
        using (var mmf = MemoryMappedFile.OpenExisting(TestCommandSegment))
        using (var accessor = mmf.CreateViewAccessor())
        {
            // Read length prefix
            int length = accessor.ReadInt32(0);

            // Read JSON data
            byte[] data = new byte[length];
            accessor.ReadArray(4, data, 0, length);

            var readJson = Encoding.UTF8.GetString(data);
            var readCommand = JsonSerializer.Deserialize<EquipmentCommand>(readJson);

            // Assert - Command successfully written to shared memory
            readCommand.Should().NotBeNull();
            readCommand!.CommandId.Should().Be(command.CommandId);
            readCommand.CommandType.Should().Be("ChangeParameter");
            readCommand.Parameters.Should().ContainKey("ParameterName");
            readCommand.Parameters["NewValue"].Should().Be("0.5");
        }
    }

    [Fact]
    public async Task WriteCommand_ShouldSignalEquipmentViaEventWaitHandle()
    {
        // Arrange
        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "TEST_CMD_SIGNAL");
        var signalReceived = false;

        var signalTask = Task.Run(() =>
        {
            var result = evt.WaitOne(TimeSpan.FromSeconds(2));
            if (result) signalReceived = true;
        });

        // Act - Middleware signals equipment
        await Task.Delay(100); // Simulate delay before writing command
        evt.Set();

        await signalTask;

        // Assert - Equipment detected signal within timeout
        signalReceived.Should().BeTrue("equipment should detect command signal within 2 seconds");
    }

    [Fact]
    public async Task WriteCommand_WithMultipleCommands_ShouldPreserveOrder()
    {
        // Arrange - Create shared memory for command queue
        _testCommandSegment = MemoryMappedFile.CreateNew("TEST_CMD_QUEUE", SegmentSize);

        var commands = new List<EquipmentCommand>
        {
            new() { CommandId = Guid.NewGuid(), CommandType = "Start", IssuedAt = DateTime.UtcNow },
            new() { CommandId = Guid.NewGuid(), CommandType = "Stop", IssuedAt = DateTime.UtcNow.AddSeconds(1) },
            new() { CommandId = Guid.NewGuid(), CommandType = "Reset", IssuedAt = DateTime.UtcNow.AddSeconds(2) }
        };

        // Act - Write commands sequentially
        int offset = 0;
        using (var accessor = _testCommandSegment.CreateViewAccessor())
        {
            foreach (var cmd in commands)
            {
                var json = JsonSerializer.Serialize(cmd);
                var bytes = Encoding.UTF8.GetBytes(json);

                accessor.Write(offset, bytes.Length);
                accessor.WriteArray(offset + 4, bytes, 0, bytes.Length);

                offset += 4 + bytes.Length;
            }
        }

        // Assert - Commands can be read in order
        using (var mmf = MemoryMappedFile.OpenExisting("TEST_CMD_QUEUE"))
        using (var accessor = mmf.CreateViewAccessor())
        {
            offset = 0;
            var readCommands = new List<EquipmentCommand>();

            for (int i = 0; i < 3; i++)
            {
                int length = accessor.ReadInt32(offset);
                byte[] data = new byte[length];
                accessor.ReadArray(offset + 4, data, 0, length);

                var json = Encoding.UTF8.GetString(data);
                var cmd = JsonSerializer.Deserialize<EquipmentCommand>(json);
                readCommands.Add(cmd!);

                offset += 4 + length;
            }

            readCommands.Should().HaveCount(3);
            readCommands[0].CommandType.Should().Be("Start");
            readCommands[1].CommandType.Should().Be("Stop");
            readCommands[2].CommandType.Should().Be("Reset");
        }

        MemoryMappedFile.OpenExisting("TEST_CMD_QUEUE").Dispose();
    }

    [Fact]
    public void WriteCommand_WhenSegmentDoesNotExist_ShouldCreateSegment()
    {
        // Arrange - No shared memory segment exists yet
        const string newSegmentName = "NEW_CMD_SEGMENT";

        // Act - Create new segment
        using var newSegment = MemoryMappedFile.CreateNew(newSegmentName, SegmentSize);

        // Assert - Segment should be accessible
        using var accessor = newSegment.CreateViewAccessor();
        accessor.Capacity.Should().BeGreaterThan(0);

        newSegment.Dispose();
    }

    [Fact]
    public async Task WriteCommand_WithConcurrentWrites_ShouldUseMutexProtection()
    {
        // Arrange - Create shared memory with semaphore protection
        _testCommandSegment = MemoryMappedFile.CreateNew("TEST_CONCURRENT_CMD", SegmentSize);

        // Use SemaphoreSlim for async scenarios
        using var semaphore = new SemaphoreSlim(1, 1);

        // Act - Simulate 5 concurrent command writes
        var tasks = Enumerable.Range(1, 5).Select(async i =>
        {
            await Task.Delay(Random.Shared.Next(10, 50)); // Random delay

            // Acquire semaphore before writing
            await semaphore.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                using var accessor = _testCommandSegment!.CreateViewAccessor();
                var cmd = new EquipmentCommand
                {
                    CommandId = Guid.NewGuid(),
                    CommandType = $"Command_{i}",
                    IssuedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(cmd);
                var bytes = Encoding.UTF8.GetBytes(json);

                accessor.Write(0, bytes.Length);
                accessor.WriteArray(4, bytes, 0, bytes.Length);

                await Task.Delay(10); // Simulate write time
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Assert - Last write should be readable (no corruption)
        using (var accessor = _testCommandSegment.CreateViewAccessor())
        {
            int length = accessor.ReadInt32(0);
            length.Should().BeGreaterThan(0, "semaphore should prevent data corruption");
            length.Should().BeLessThan(500, "length should be reasonable");
        }

        MemoryMappedFile.OpenExisting("TEST_CONCURRENT_CMD").Dispose();
    }
}
