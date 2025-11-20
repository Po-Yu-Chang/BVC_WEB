using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Services.SharedMemory;

/// <summary>
/// Writes commands to shared memory for equipment to read.
/// Part of User Story 3 (Bidirectional Command & Control) - T062.
/// </summary>
public class SharedMemoryWriter : ISharedMemoryWriter
{
    private const string CommandSegmentName = "MES_EQUIPMENT_CMD";
    private const string AckSegmentName = "MES_EQUIPMENT_CMD_ACK";
    private const string CommandEventName = "MES_CMD_READY";
    private const string AckEventName = "MES_ACK_READY";
    private const long SegmentSize = 10 * 1024 * 1024; // 10MB
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<SharedMemoryWriter> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SharedMemoryWriter(ILogger<SharedMemoryWriter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Writes a command to shared memory for equipment to read.
    /// </summary>
    public async Task<bool> WriteCommandAsync(EquipmentCommand command, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Writing command {CommandId} ({CommandType}) to shared memory",
                command.CommandId, command.CommandType);

            // Serialize command to JSON
            var json = JsonSerializer.Serialize(command);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            _logger.LogDebug("Command JSON size: {Size} bytes", jsonBytes.Length);

            // Write to shared memory
            using var mmf = MemoryMappedFile.CreateOrOpen(CommandSegmentName, SegmentSize);
            using var accessor = mmf.CreateViewAccessor();

            // Write length prefix (4 bytes)
            accessor.Write(0, jsonBytes.Length);

            // Write JSON data
            accessor.WriteArray(4, jsonBytes, 0, jsonBytes.Length);

            _logger.LogDebug("Command written to shared memory at offset 0, length {Length}", jsonBytes.Length);

            // Signal equipment via EventWaitHandle
            using var cmdEvent = new EventWaitHandle(false, EventResetMode.AutoReset, CommandEventName);
            cmdEvent.Set();

            _logger.LogInformation("Command {CommandId} written successfully, equipment signaled", command.CommandId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write command {CommandId} to shared memory", command.CommandId);
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Waits for equipment to acknowledge command execution.
    /// </summary>
    public async Task<CommandAcknowledgment?> WaitForAcknowledgmentAsync(Guid commandId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var actualTimeout = timeout ?? DefaultTimeout;
        _logger.LogInformation("Waiting for acknowledgment of command {CommandId} (timeout: {Timeout}s)",
            commandId, actualTimeout.TotalSeconds);

        try
        {
            using var ackEvent = new EventWaitHandle(false, EventResetMode.AutoReset, AckEventName);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(actualTimeout);

            // Wait for acknowledgment signal with timeout
            var waitTask = Task.Run(() =>
            {
                try
                {
                    var waitHandles = new[] { ackEvent, cts.Token.WaitHandle };
                    var index = WaitHandle.WaitAny(waitHandles, actualTimeout);
                    return index == 0; // True if ackEvent signaled, false if timeout/cancelled
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }, cts.Token);

            var signaled = await waitTask;

            if (!signaled)
            {
                _logger.LogWarning("Command {CommandId} acknowledgment timeout after {Timeout}s",
                    commandId, actualTimeout.TotalSeconds);
                return null;
            }

            // Read acknowledgment from shared memory
            using var mmf = MemoryMappedFile.OpenExisting(AckSegmentName);
            using var accessor = mmf.CreateViewAccessor();

            int length = accessor.ReadInt32(0);
            if (length <= 0 || length > 1024 * 1024) // Sanity check
            {
                _logger.LogWarning("Invalid acknowledgment length: {Length}", length);
                return null;
            }

            byte[] data = new byte[length];
            accessor.ReadArray(4, data, 0, length);

            var json = Encoding.UTF8.GetString(data);
            var ack = JsonSerializer.Deserialize<CommandAcknowledgment>(json);

            if (ack == null)
            {
                _logger.LogWarning("Failed to deserialize acknowledgment for command {CommandId}", commandId);
                return null;
            }

            _logger.LogInformation("Received acknowledgment for command {CommandId}: Status={Status}, Message={Message}",
                commandId, ack.Status, ack.Message);

            return ack;
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Acknowledgment segment not found for command {CommandId}", commandId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for acknowledgment of command {CommandId}", commandId);
            return null;
        }
    }
}
