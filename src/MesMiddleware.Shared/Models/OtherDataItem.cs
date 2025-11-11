namespace MesMiddleware.Shared.Models;

/// <summary>
/// Other metadata item (part of MES three-tier data model).
/// Contains additional context like timestamps, environmental conditions, equipment settings.
/// </summary>
public class OtherDataItem
{
    /// <summary>
    /// Metadata key/identifier (e.g., "Temperature", "Humidity", "EquipmentMode")
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Metadata value
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Data type hint (e.g., "string", "number", "datetime", "boolean")
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Unit of measurement (if applicable)
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Additional description or notes
    /// </summary>
    public string? Description { get; set; }
}
