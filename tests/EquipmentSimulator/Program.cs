using MesMiddleware.Shared.Models;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;

Console.WriteLine("==============================================");
Console.WriteLine("Equipment Simulator - MES Middleware Testing");
Console.WriteLine("==============================================");
Console.WriteLine();

const string SEGMENT_NAME = "MES_INSPECTION_DATA";
const string EVENT_NAME = "MES_DATA_READY";
const string MUTEX_NAME = "MES_INSPECTION_DATA_MUTEX";
const long SEGMENT_SIZE = 10 * 1024 * 1024; // 10MB

Console.WriteLine($"Shared Memory Segment: {SEGMENT_NAME}");
Console.WriteLine($"Event Signal: {EVENT_NAME}");
Console.WriteLine($"Mutex: {MUTEX_NAME}");
Console.WriteLine();

// Create or open shared memory segment
MemoryMappedFile? segment = null;
EventWaitHandle? eventSignal = null;
Mutex? mutex = null;

try
{
    segment = MemoryMappedFile.CreateOrOpen(SEGMENT_NAME, SEGMENT_SIZE);
    eventSignal = new EventWaitHandle(false, EventResetMode.AutoReset, EVENT_NAME);
    mutex = new Mutex(false, MUTEX_NAME);

    Console.WriteLine("✓ Shared memory initialized successfully");
    Console.WriteLine();

    while (true)
    {
        Console.WriteLine("Options:");
        Console.WriteLine("  1. Send single inspection record");
        Console.WriteLine("  2. Send batch (10 records)");
        Console.WriteLine("  3. Send continuous stream (1 record/second)");
        Console.WriteLine("  4. Exit");
        Console.Write("Select option: ");

        var option = Console.ReadLine();

        switch (option)
        {
            case "1":
                SendRecord(segment, eventSignal, mutex);
                break;
            case "2":
                SendBatch(segment, eventSignal, mutex, 10);
                break;
            case "3":
                await SendContinuousAsync(segment, eventSignal, mutex);
                break;
            case "4":
                Console.WriteLine("Exiting...");
                return;
            default:
                Console.WriteLine("Invalid option");
                break;
        }

        Console.WriteLine();
    }
}
finally
{
    mutex?.Dispose();
    eventSignal?.Dispose();
    segment?.Dispose();
}

static void SendRecord(MemoryMappedFile segment, EventWaitHandle eventSignal, Mutex mutex)
{
    try
    {
        var record = GenerateInspectionRecord();

        Console.Write($"Sending record {record.RowNo}... ");

        WriteToSharedMemory(segment, mutex, record);
        eventSignal.Set();

        Console.WriteLine("✓ Sent");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ Error: {ex.Message}");
    }
}

static void SendBatch(MemoryMappedFile segment, EventWaitHandle eventSignal, Mutex mutex, int count)
{
    Console.WriteLine($"Sending batch of {count} records...");

    for (int i = 0; i < count; i++)
    {
        var record = GenerateInspectionRecord();
        Console.Write($"  {i + 1}/{count}: Record {record.RowNo}... ");

        try
        {
            WriteToSharedMemory(segment, mutex, record);
            eventSignal.Set();
            Console.WriteLine("✓");
            Thread.Sleep(100); // Small delay between records
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
        }
    }

    Console.WriteLine($"✓ Batch complete: {count} records sent");
}

static async Task SendContinuousAsync(MemoryMappedFile segment, EventWaitHandle eventSignal, Mutex mutex)
{
    Console.WriteLine("Starting continuous stream (1 record/second)... Press Ctrl+C to stop");
    Console.WriteLine();

    int count = 0;
    while (true)
    {
        var record = GenerateInspectionRecord();
        count++;

        try
        {
            WriteToSharedMemory(segment, mutex, record);
            eventSignal.Set();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent record {count}: {record.RowNo}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
        }

        await Task.Delay(1000);
    }
}

static void WriteToSharedMemory(MemoryMappedFile segment, Mutex mutex, InspectionRecord record)
{
    // Acquire exclusive access
    if (!mutex.WaitOne(TimeSpan.FromSeconds(5)))
    {
        throw new TimeoutException("Failed to acquire mutex within 5 seconds");
    }

    try
    {
        // Serialize to JSON
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var jsonBytes = Encoding.UTF8.GetBytes(json);

        if (jsonBytes.Length > SEGMENT_SIZE - 4)
        {
            throw new InvalidOperationException($"Data size ({jsonBytes.Length} bytes) exceeds segment size");
        }

        // Write to shared memory: [4-byte length][JSON data]
        using var accessor = segment.CreateViewAccessor();
        accessor.Write(0, jsonBytes.Length);
        accessor.WriteArray(4, jsonBytes, 0, jsonBytes.Length);
    }
    finally
    {
        mutex.ReleaseMutex();
    }
}

static InspectionRecord GenerateInspectionRecord()
{
    var timestamp = DateTime.UtcNow;
    var rowNo = $"{timestamp:yyyyMMddHHmmssfff}";

    return new InspectionRecord
    {
        RowNo = rowNo,
        ProcName = "Final Inspection",
        DevName = $"VISION_SYSTEM_{Random.Shared.Next(1, 4):D2}",
        UserName = $"OP{Random.Shared.Next(1, 10):D3}",
        WorkClass = Random.Shared.Next(0, 2) == 0 ? "A" : "B",
        TraceCode = $"TR{timestamp:yyyyMMddHHmmss}",
        LotNo = null,
        InspectionTime = timestamp,
        ParamData = new List<ParamDataItem>
        {
            new ParamDataItem
            {
                Name = "Dimension_X",
                Value = (100.0 + Random.Shared.NextDouble() * 2 - 1).ToString("F2"),
                Unit = "mm",
                Status = "OK"
            },
            new ParamDataItem
            {
                Name = "Dimension_Y",
                Value = (50.0 + Random.Shared.NextDouble() * 2 - 1).ToString("F2"),
                Unit = "mm",
                Status = "OK"
            },
            new ParamDataItem
            {
                Name = "Surface_Defects",
                Value = Random.Shared.Next(0, 3).ToString(),
                Unit = "count",
                Status = Random.Shared.Next(0, 10) < 9 ? "OK" : "NG"
            }
        },
        Benchmarks = new List<BenchmarkItem>
        {
            new BenchmarkItem
            {
                Name = "Dimension_X",
                UpperLimit = "101.0",
                LowerLimit = "99.0",
                Unit = "mm"
            },
            new BenchmarkItem
            {
                Name = "Dimension_Y",
                UpperLimit = "51.0",
                LowerLimit = "49.0",
                Unit = "mm"
            },
            new BenchmarkItem
            {
                Name = "Surface_Defects",
                UpperLimit = "2",
                LowerLimit = "0",
                Unit = "count"
            }
        },
        OtherData = new List<OtherDataItem>
        {
            new OtherDataItem
            {
                Key = "Temperature",
                Value = (25.0 + Random.Shared.NextDouble() * 5).ToString("F1"),
                Unit = "°C"
            },
            new OtherDataItem
            {
                Key = "Humidity",
                Value = (40.0 + Random.Shared.NextDouble() * 20).ToString("F1"),
                Unit = "%"
            }
        }
    };
}
