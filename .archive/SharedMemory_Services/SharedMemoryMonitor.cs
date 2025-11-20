using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MesMiddleware.Service.Models;
using MesMiddleware.Shared.Models;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;

namespace MesMiddleware.Service.Services.SharedMemory;

/// <summary>
/// Shared memory monitor implementation using MemoryMappedFile + EventWaitHandle.
/// Monitors equipment inspection data segment, signals when data ready.
/// Thread-safe with Mutex protection for concurrent access.
/// </summary>
public class SharedMemoryMonitor : ISharedMemoryMonitor
{
    private readonly SharedMemoryOptions _options;
    private readonly ILogger<SharedMemoryMonitor> _logger;

    private MemoryMappedFile? _inspectionDataSegment;
    private EventWaitHandle? _dataReadyEvent;
    private Mutex? _accessMutex;
    private Task? _monitoringTask;
    private CancellationTokenSource? _stoppingCts;

    public event EventHandler<InspectionDataReceivedEventArgs>? DataReceived;

    public SharedMemoryMonitor(
        IOptions<SharedMemoryOptions> options,
        ILogger<SharedMemoryMonitor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting shared memory monitor for segment '{SegmentName}'",
            _options.InspectionDataSegmentName);

        try
        {
            // Create or open shared memory segment
            try
            {
                _inspectionDataSegment = MemoryMappedFile.OpenExisting(_options.InspectionDataSegmentName);
                _logger.LogInformation("Opened existing shared memory segment");
            }
            catch (FileNotFoundException)
            {
                _logger.LogInformation("Creating new shared memory segment (size: {Size} bytes)",
                    _options.SegmentSize);
                _inspectionDataSegment = MemoryMappedFile.CreateNew(
                    _options.InspectionDataSegmentName,
                    _options.SegmentSize);
            }

            // Create or open event wait handle for data ready signal
            _dataReadyEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                _options.DataReadyEventName);

            // Create mutex for concurrent access protection
            _accessMutex = new Mutex(false, $"{_options.InspectionDataSegmentName}_MUTEX");

            _stoppingCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stoppingCts.Token);

            // Start monitoring loop
            _monitoringTask = Task.Run(() => MonitoringLoopAsync(linkedCts.Token), linkedCts.Token);

            _logger.LogInformation("Shared memory monitor started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start shared memory monitor");
            throw;
        }
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping shared memory monitor");

        _stoppingCts?.Cancel();

        if (_monitoringTask != null)
        {
            await _monitoringTask;
        }

        _dataReadyEvent?.Dispose();
        _accessMutex?.Dispose();
        _inspectionDataSegment?.Dispose();

        _logger.LogInformation("Shared memory monitor stopped");
    }

    private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monitoring loop started, waiting for equipment data signals");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait for equipment to signal data ready (blocking call)
                var signalReceived = _dataReadyEvent!.WaitOne(TimeSpan.FromSeconds(1));

                if (!signalReceived)
                {
                    // Timeout - continue loop (allows checking cancellation token)
                    continue;
                }

                _logger.LogDebug("Data ready signal received from equipment");

                // Read data from shared memory with mutex protection
                var data = ReadInspectionDataFromSharedMemory();

                if (data != null)
                {
                    // Raise event for subscribers (MiddlewareHostedService)
                    OnDataReceived(data);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in shared memory monitoring loop");
                // Continue monitoring despite errors
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        _logger.LogInformation("Monitoring loop exited");
    }

    private InspectionDataReceivedEventArgs? ReadInspectionDataFromSharedMemory()
    {
        try
        {
            // Acquire mutex for exclusive access
            if (!_accessMutex!.WaitOne(TimeSpan.FromMilliseconds(_options.MutexTimeout)))
            {
                _logger.LogWarning("Failed to acquire mutex within {Timeout}ms, skipping read",
                    _options.MutexTimeout);
                return null;
            }

            try
            {
                using var accessor = _inspectionDataSegment!.CreateViewAccessor();

                // Read length prefix (first 4 bytes)
                int dataLength = accessor.ReadInt32(0);

                if (dataLength <= 0 || dataLength > _options.SegmentSize - 4)
                {
                    _logger.LogWarning("Invalid data length: {Length} bytes (expected 1 to {MaxLength})",
                        dataLength,
                        _options.SegmentSize - 4);
                    return null;
                }

                // Read JSON data
                byte[] jsonBytes = new byte[dataLength];
                accessor.ReadArray(4, jsonBytes, 0, dataLength);

                var json = Encoding.UTF8.GetString(jsonBytes);

                _logger.LogDebug("Read {Size} bytes from shared memory", dataLength);

                // Deserialize JSON to InspectionRecord
                var record = JsonSerializer.Deserialize<InspectionRecord>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (record == null)
                {
                    _logger.LogError("Failed to deserialize JSON from shared memory");
                    return null;
                }

                _logger.LogInformation("Successfully read inspection data from shared memory: {TraceCodeOrLot} (RowNo: {RowNo})",
                    record.TraceCode ?? record.LotNo,
                    record.RowNo);

                return new InspectionDataReceivedEventArgs
                {
                    Data = record,
                    ReceivedAt = DateTime.UtcNow,
                    DataSizeBytes = dataLength
                };
            }
            finally
            {
                _accessMutex.ReleaseMutex();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON from shared memory");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading data from shared memory");
            return null;
        }
    }

    private void OnDataReceived(InspectionDataReceivedEventArgs eventArgs)
    {
        try
        {
            DataReceived?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DataReceived event handler");
        }
    }
}
