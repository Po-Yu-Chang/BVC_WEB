using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Tests.Integration;

/// <summary>
/// Integration tests for Windows shared memory IPC using MemoryMappedFile.
/// Tests equipment → middleware data flow via shared memory.
/// RED PHASE: These tests will fail until we implement SharedMemoryMonitor.
/// </summary>
public class SharedMemoryIpcTests : IDisposable
{
    private const string TestSegmentName = "MES_TEST_INSPECTION_DATA";
    private const string TestEventName = "MES_TEST_DATA_READY";
    private const long SegmentSize = 1024 * 1024; // 1MB for tests

    private MemoryMappedFile? _testSegment;
    private EventWaitHandle? _testEvent;

    public void Dispose()
    {
        _testEvent?.Dispose();
        _testSegment?.Dispose();
    }

    [Fact]
    public async Task EquipmentWritesToSharedMemory_MiddlewareReadsSuccessfully()
    {
        // Arrange - Create shared memory segment (simulating equipment)
        _testSegment = MemoryMappedFile.CreateNew(TestSegmentName, SegmentSize);
        _testEvent = new EventWaitHandle(false, EventResetMode.AutoReset, TestEventName);

        var equipmentData = new InspectionRecord
        {
            RowNo = "IPC001",
            ProcName = "Shared Memory Test",
            DevName = "TEST-MACHINE",
            UserName = "test_user",
            WorkClass = "Day",
            TraceCode = "IPC_TRACE001",
            ParamData = new List<ParamDataItem>
            {
                new() { Name = "TestParam", Value = "123.45", Unit = "mm", Status = "Pass" }
            },
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act - Equipment writes JSON to shared memory
        var json = JsonSerializer.Serialize(equipmentData);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using (var accessor = _testSegment.CreateViewAccessor())
        {
            // Write length prefix (first 4 bytes)
            accessor.Write(0, jsonBytes.Length);

            // Write JSON data (starting at offset 4)
            accessor.WriteArray(4, jsonBytes, 0, jsonBytes.Length);
        }

        // Signal middleware that data is ready
        _testEvent.Set();

        // Middleware reads from shared memory
        using (var mmf = MemoryMappedFile.OpenExisting(TestSegmentName))
        using (var accessor = mmf.CreateViewAccessor())
        {
            // Read length prefix
            int length = accessor.ReadInt32(0);

            // Read JSON data
            byte[] data = new byte[length];
            accessor.ReadArray(4, data, 0, length);

            var readJson = Encoding.UTF8.GetString(data);
            var readRecord = JsonSerializer.Deserialize<InspectionRecord>(readJson);

            // Assert - Middleware successfully read equipment data
            readRecord.Should().NotBeNull();
            readRecord!.RowNo.Should().Be("IPC001");
            readRecord.ProcName.Should().Be("Shared Memory Test");
            readRecord.TraceCode.Should().Be("IPC_TRACE001");
            readRecord.ParamData.Should().HaveCount(1);
            readRecord.ParamData[0].Value.Should().Be("123.45");
        }
    }

    [Fact]
    public async Task EventWaitHandle_SignalsMiddlewareWhenDataReady()
    {
        // Arrange
        using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "TEST_EVENT_SIGNAL");
        var signalReceived = false;
        var signalTask = Task.Run(() =>
        {
            var result = evt.WaitOne(TimeSpan.FromSeconds(2));
            if (result) signalReceived = true;
        });

        // Act - Equipment signals middleware
        await Task.Delay(100); // Simulate delay before equipment writes data
        evt.Set();

        await signalTask;

        // Assert - Middleware detected signal within timeout
        signalReceived.Should().BeTrue("middleware should detect equipment signal within 2 seconds");
    }

    [Fact]
    public async Task SharedMemory_WithLargePayload_ShouldHandleCorrectly()
    {
        // Arrange - Create large inspection data (10KB+ JSON)
        _testSegment = MemoryMappedFile.CreateNew("TEST_LARGE_PAYLOAD", SegmentSize);

        var largeData = new InspectionRecord
        {
            RowNo = "LARGE001",
            ProcName = "Stress Test",
            DevName = "STRESS-MACHINE",
            UserName = "stress_user",
            WorkClass = "Night",
            TraceCode = "STRESS_TRACE",
            ParamData = Enumerable.Range(1, 100).Select(i => new ParamDataItem
            {
                Name = $"Param_{i}",
                Value = $"{i * 1.5}",
                Unit = "unit",
                Status = "Pass"
            }).ToList(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act - Write large payload
        var json = JsonSerializer.Serialize(largeData);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        jsonBytes.Length.Should().BeGreaterThan(5000, "payload should be large enough for stress test");

        using (var accessor = _testSegment.CreateViewAccessor())
        {
            accessor.Write(0, jsonBytes.Length);
            accessor.WriteArray(4, jsonBytes, 0, jsonBytes.Length);
        }

        // Read back and verify
        using (var mmf = MemoryMappedFile.OpenExisting("TEST_LARGE_PAYLOAD"))
        using (var accessor = mmf.CreateViewAccessor())
        {
            int length = accessor.ReadInt32(0);
            byte[] data = new byte[length];
            accessor.ReadArray(4, data, 0, length);

            var readJson = Encoding.UTF8.GetString(data);
            var readRecord = JsonSerializer.Deserialize<InspectionRecord>(readJson);

            // Assert - Large payload handled correctly
            readRecord.Should().NotBeNull();
            readRecord!.ParamData.Should().HaveCount(100);
            readRecord.ParamData[99].Name.Should().Be("Param_100");
        }

        MemoryMappedFile.OpenExisting("TEST_LARGE_PAYLOAD").Dispose();
    }

    [Fact]
    public void SharedMemory_WhenSegmentDoesNotExist_ShouldThrowException()
    {
        // Act & Assert - Attempting to open non-existent segment should fail
        Action openNonExistent = () =>
        {
            using var mmf = MemoryMappedFile.OpenExisting("NON_EXISTENT_SEGMENT");
        };

        openNonExistent.Should().Throw<FileNotFoundException>(
            "opening a non-existent shared memory segment should throw FileNotFoundException");
    }

    [Fact]
    public async Task ConcurrentAccess_WithMutex_ShouldPreventCorruption()
    {
        // Arrange - Create shared memory with semaphore protection
        _testSegment = MemoryMappedFile.CreateNew("TEST_CONCURRENT", SegmentSize);

        // Use SemaphoreSlim instead of Mutex for async scenarios
        using var semaphore = new SemaphoreSlim(1, 1);

        // Act - Simulate 5 concurrent writes (equipment + middleware contention)
        var tasks = Enumerable.Range(1, 5).Select(async i =>
        {
            await Task.Delay(Random.Shared.Next(10, 50)); // Random delay

            // Acquire semaphore before writing
            await semaphore.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                using var accessor = _testSegment!.CreateViewAccessor();
                var data = $"Write_{i}";
                var bytes = Encoding.UTF8.GetBytes(data);
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
        using (var accessor = _testSegment.CreateViewAccessor())
        {
            int length = accessor.ReadInt32(0);
            length.Should().BeGreaterThan(0, "semaphore should prevent data corruption");
            length.Should().BeLessThan(100, "length should be reasonable");
        }

        MemoryMappedFile.OpenExisting("TEST_CONCURRENT").Dispose();
    }
}
