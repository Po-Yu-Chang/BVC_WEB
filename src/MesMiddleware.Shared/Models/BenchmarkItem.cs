namespace MesMiddleware.Shared.Models;

/// <summary>
/// Benchmark/specification limit item (part of MES three-tier data model).
/// Contains upper/lower limits, thresholds, and acceptance criteria.
/// </summary>
public class BenchmarkItem
{
    /// <summary>
    /// Benchmark name/identifier (corresponds to ParamDataItem)
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Upper specification limit (maximum acceptable value)
    /// </summary>
    public string? UpperLimit { get; set; }

    /// <summary>
    /// Lower specification limit (minimum acceptable value)
    /// </summary>
    public string? LowerLimit { get; set; }

    /// <summary>
    /// Target/nominal value
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Unit of measurement (should match ParamDataItem unit)
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Tolerance value (acceptable deviation from target)
    /// </summary>
    public string? Tolerance { get; set; }

    /// <summary>
    /// Additional notes about this benchmark
    /// </summary>
    public string? Notes { get; set; }
}
