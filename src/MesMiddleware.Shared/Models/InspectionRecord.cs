namespace MesMiddleware.Shared.Models;

/// <summary>
/// Complete inspection record matching WebAPI InspectionDataRequest schema.
/// Represents equipment inspection data to be uploaded to MES system.
/// </summary>
public class InspectionRecord
{
    /// <summary>
    /// Process name (e.g., "Blind Hole Inspection", "AOI", "AVI")
    /// </summary>
    public required string ProcName { get; set; }

    /// <summary>
    /// Device/Equipment name that performed the inspection
    /// </summary>
    public required string DevName { get; set; }

    /// <summary>
    /// Operator username who initiated or supervised the inspection
    /// </summary>
    public required string UserName { get; set; }

    /// <summary>
    /// Work class/shift (e.g., "Day", "Night", "Morning")
    /// </summary>
    public required string WorkClass { get; set; }

    /// <summary>
    /// Trace code for individual product tracking (required if LotNo is null)
    /// </summary>
    public string? TraceCode { get; set; }

    /// <summary>
    /// Lot number for batch tracking (required if TraceCode is null)
    /// </summary>
    public string? LotNo { get; set; }

    /// <summary>
    /// Part number (e.g., "3FIA98338D01")
    /// </summary>
    public string? PartNumber { get; set; }

    /// <summary>
    /// Remarks/notes
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// Measured parameter data (measurements, values, results)
    /// </summary>
    public List<ParamDataItem> ParamData { get; set; } = new();

    /// <summary>
    /// Benchmark/specification limits (upper/lower limits, thresholds)
    /// </summary>
    public List<BenchmarkItem> Benchmarks { get; set; } = new();

    /// <summary>
    /// Other metadata (timestamps, environmental conditions, equipment settings)
    /// </summary>
    public List<OtherDataItem> OtherData { get; set; } = new();

    /// <summary>
    /// Inspection timestamp (when the inspection was performed)
    /// </summary>
    public DateTime InspectionTime { get; set; } = DateTime.UtcNow;
}
