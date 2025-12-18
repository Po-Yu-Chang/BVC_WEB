using System.Text.Json.Serialization;

namespace MesMiddleware.Shared.Models.MesApi;

/// <summary>
/// MES Cloud API trace data upload request.
/// Matches the exact format required by: POST /CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3
/// </summary>
public class MesTraceDataRequest
{
    /// <summary>
    /// Whether to verify lot (always false for this equipment)
    /// </summary>
    [JsonPropertyName("isVerifyLot")]
    public bool IsVerifyLot { get; set; } = false;

    /// <summary>
    /// Array of inspection data records
    /// </summary>
    [JsonPropertyName("data")]
    public List<MesTraceData> Data { get; set; } = new();
}

/// <summary>
/// Single trace data record for MES Cloud API.
/// </summary>
public class MesTraceData
{
    /// <summary>
    /// Device code (e.g., "W3-WTYKJ-001")
    /// </summary>
    [JsonPropertyName("devName")]
    public string DevName { get; set; } = string.Empty;

    /// <summary>
    /// Process code (e.g., "W3-ET")
    /// </summary>
    [JsonPropertyName("procName")]
    public string ProcName { get; set; } = string.Empty;

    /// <summary>
    /// Operator username (e.g., "53900")
    /// </summary>
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Work shift class (e.g., "A")
    /// </summary>
    [JsonPropertyName("workClass")]
    public string WorkClass { get; set; } = string.Empty;

    /// <summary>
    /// Trace code for product tracking (e.g., "O3781233T99230030101")
    /// </summary>
    [JsonPropertyName("traceCode")]
    public string TraceCode { get; set; } = string.Empty;

    /// <summary>
    /// Lot number (e.g., "02029156-00800-N")
    /// </summary>
    [JsonPropertyName("lotNo")]
    public string LotNo { get; set; } = string.Empty;

    /// <summary>
    /// Part number (e.g., "3FIA98338D01")
    /// </summary>
    [JsonPropertyName("partNumber")]
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>
    /// Remarks/notes
    /// </summary>
    [JsonPropertyName("remark")]
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// Measured parameter data (Result, DefectQty, OkQty, CheckQty)
    /// </summary>
    [JsonPropertyName("paramData")]
    public List<MesParamDataItem> ParamData { get; set; } = new();

    /// <summary>
    /// Benchmark data (Defect_Qty_XX, Check_Param_XX, measurement values)
    /// </summary>
    [JsonPropertyName("benchmarks")]
    public List<MesBenchmarkItem> Benchmarks { get; set; } = new();

    /// <summary>
    /// Other data (CheckTime)
    /// </summary>
    [JsonPropertyName("otherData")]
    public List<MesOtherDataItem> OtherData { get; set; } = new();
}

/// <summary>
/// Parameter data item for MES API.
/// Required fields: Result, DefectQty, OkQty, CheckQty
/// </summary>
public class MesParamDataItem
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;
}

/// <summary>
/// Benchmark item for MES API.
/// For defect quantities: code format "Defect_Qty_XX"
/// For check parameters: code format "Check_Param_XX"
/// </summary>
public class MesBenchmarkItem
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;
}

/// <summary>
/// Other data item for MES API.
/// Required field: CheckTime (format: yyyy-MM-dd HH:mm:ss)
/// </summary>
public class MesOtherDataItem
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;
}
