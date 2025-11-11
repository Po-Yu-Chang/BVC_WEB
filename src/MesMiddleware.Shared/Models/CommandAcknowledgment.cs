namespace MesMiddleware.Shared.Models;

/// <summary>
/// Represents equipment's acknowledgment of a command received via shared memory.
/// Part of User Story 3 (Bidirectional Command & Control).
/// </summary>
public class CommandAcknowledgment
{
    /// <summary>
    /// CommandId from the original EquipmentCommand (used to match acknowledgment to command).
    /// </summary>
    public Guid CommandId { get; set; }

    /// <summary>
    /// Status of command execution: "Success", "Failed", "Timeout", "Pending".
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Optional message from equipment (e.g., "Calibration completed" or "Sensor error during execution").
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// UTC timestamp when equipment acknowledged the command (or when middleware detected timeout).
    /// </summary>
    public DateTime AcknowledgedAt { get; set; }
}
