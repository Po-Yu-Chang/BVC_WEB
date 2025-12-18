using System.Text.Json.Serialization;

namespace MesMiddleware.Shared.Models.LabView;

/// <summary>
/// LabVIEW inspection request wrapper.
/// Matches the exact JSON structure sent by LabVIEW equipment.
/// </summary>
public class LabViewInspectionRequest
{
    /// <summary>
    /// Whether to verify lot (always false for this equipment)
    /// </summary>
    [JsonPropertyName("isVerifyLot")]
    public bool IsVerifyLot { get; set; }

    /// <summary>
    /// Array of inspection data records
    /// </summary>
    [JsonPropertyName("data")]
    public List<LabViewInspectionData> Data { get; set; } = new();
}

/// <summary>
/// Single inspection data record from LabVIEW.
/// Contains all measurement data for one inspection.
/// </summary>
public class LabViewInspectionData
{
    [JsonPropertyName("rowNo")]
    public string? RowNo { get; set; }

    [JsonPropertyName("procName")]
    public string? ProcName { get; set; }

    [JsonPropertyName("devName")]
    public string? DevName { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("workClass")]
    public string? WorkClass { get; set; }

    [JsonPropertyName("traceCode")]
    public string? TraceCode { get; set; }

    [JsonPropertyName("lotNo")]
    public string? LotNo { get; set; }

    [JsonPropertyName("partNumber")]
    public string? PartNumber { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("paramData")]
    public List<LabViewParamDataItem> ParamData { get; set; } = new();

    [JsonPropertyName("benchmarks")]
    public List<LabViewBenchmarkItem> Benchmarks { get; set; } = new();

    [JsonPropertyName("otherData")]
    public List<LabViewOtherDataItem> OtherData { get; set; } = new();
}

/// <summary>
/// Parameter data item from LabVIEW.
/// Contains measured values like CheckQty, DefectQty, OkQty, etc.
/// </summary>
public class LabViewParamDataItem
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }
}

/// <summary>
/// Benchmark item from LabVIEW.
/// Contains check parameters and measurement details.
/// </summary>
public class LabViewBenchmarkItem
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }
}

/// <summary>
/// Other data item from LabVIEW.
/// Contains additional metadata like CheckTime.
/// </summary>
public class LabViewOtherDataItem
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }
}
