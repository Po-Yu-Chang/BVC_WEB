namespace MesMiddleware.Shared.Models;

/// <summary>
/// Represents a command sent from WebAPI/middleware to equipment via shared memory.
/// Part of User Story 3 (Bidirectional Command & Control).
/// </summary>
public class EquipmentCommand
{
    /// <summary>
    /// Unique identifier for this command (used to track acknowledgment).
    /// </summary>
    public Guid CommandId { get; set; }

    /// <summary>
    /// Type of command (e.g., "ChangeParameter", "Calibrate", "Start", "Stop", "Reset").
    /// </summary>
    public string CommandType { get; set; } = string.Empty;

    /// <summary>
    /// Command parameters as key-value pairs (e.g., { "ParameterName": "Threshold", "NewValue": "0.5" }).
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// UTC timestamp when command was issued by WebAPI/middleware.
    /// </summary>
    public DateTime IssuedAt { get; set; }
}
