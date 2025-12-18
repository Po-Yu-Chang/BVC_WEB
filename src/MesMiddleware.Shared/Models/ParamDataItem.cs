namespace MesMiddleware.Shared.Models;

/// <summary>
/// Measured parameter data item (part of MES three-tier data model).
/// Contains inspection measurement values and results.
/// </summary>
public class ParamDataItem
{
    /// <summary>
    /// Parameter code/identifier (e.g., "Result", "DefectQty", "OkQty", "CheckQty")
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Parameter name/identifier (e.g., "HoleDiameter", "Depth", "Position")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Measured value as string (flexible for different data types)
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Unit of measurement (e.g., "mm", "μm", "degrees", "count")
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Measurement status (e.g., "Pass", "Fail", "Warning", "OK")
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Description/notes about this measurement
    /// </summary>
    public string? Desc { get; set; }
}
