namespace MesMiddleware.Monitor.Models;

/// <summary>
/// Represents a command record for display in WPF Command History view.
/// Part of User Story 3 (Bidirectional Command & Control) - T065-T066.
/// </summary>
public class CommandRecord
{
    /// <summary>
    /// Unique command identifier.
    /// </summary>
    public Guid CommandId { get; set; }

    /// <summary>
    /// Timestamp when command was issued.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Type of command (e.g., "ChangeParameter", "Calibrate", "Start", "Stop").
    /// </summary>
    public string CommandType { get; set; } = string.Empty;

    /// <summary>
    /// Equipment name that received the command.
    /// </summary>
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>
    /// Command execution status: "Pending", "Success", "Failed", "Timeout".
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Error or acknowledgment message from equipment.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Command parameters as formatted string (for display).
    /// </summary>
    public string? Parameters { get; set; }
}
