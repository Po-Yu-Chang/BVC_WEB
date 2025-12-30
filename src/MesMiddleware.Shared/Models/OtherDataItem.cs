namespace MesMiddleware.Shared.Models;

/// <summary>
/// Other metadata item (part of MES three-tier data model).
/// Contains additional context like timestamps, environmental conditions, equipment settings.
/// </summary>
public class OtherDataItem
{
    /// <summary>
    /// Code/identifier (e.g., "CheckTime")
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Metadata name/identifier (e.g., "CheckTime", "Temperature")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Metadata value
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Unit of measurement (if applicable)
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Description/notes
    /// </summary>
    public string? Desc { get; set; }
}
