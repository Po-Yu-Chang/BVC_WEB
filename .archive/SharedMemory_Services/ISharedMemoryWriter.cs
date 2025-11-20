using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Services.SharedMemory;

/// <summary>
/// Interface for writing commands to shared memory (equipment-bound communication).
/// Part of User Story 3 (Bidirectional Command & Control).
/// </summary>
public interface ISharedMemoryWriter
{
    /// <summary>
    /// Writes a command to shared memory for equipment to read.
    /// Uses MemoryMappedFile with "MES_EQUIPMENT_CMD" segment name.
    /// </summary>
    /// <param name="command">Command to write to shared memory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if command was written successfully</returns>
    Task<bool> WriteCommandAsync(EquipmentCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for equipment to acknowledge command execution.
    /// Monitors "MES_EQUIPMENT_CMD_ACK" segment with 30-second timeout.
    /// </summary>
    /// <param name="commandId">ID of command to wait for</param>
    /// <param name="timeout">Timeout duration (default 30 seconds)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Acknowledgment from equipment, or null if timeout</returns>
    Task<CommandAcknowledgment?> WaitForAcknowledgmentAsync(Guid commandId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
