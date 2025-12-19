using System.Text.Json.Serialization;

namespace MesMiddleware.Simulator.Models;

#region 1. 設備登錄 API Models

/// <summary>
/// 設備登錄請求
/// </summary>
public class LoginRequest
{
    [JsonPropertyName("PrtMacNo")]
    public string PrtMacNo { get; set; } = string.Empty;
}

/// <summary>
/// 設備登錄響應數據
/// </summary>
public class LoginResponseData
{
    [JsonPropertyName("PrtMacNo")]
    public string PrtMacNo { get; set; } = string.Empty;

    [JsonPropertyName("IpAddr")]
    public string IpAddr { get; set; } = string.Empty;

    [JsonPropertyName("Token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("SysUserId")]
    public string? SysUserId { get; set; }
}

#endregion

#region 2. 數據上傳 API Models

/// <summary>
/// 數據上傳請求
/// </summary>
public class TraceDataRequest
{
    [JsonPropertyName("isVerifyLot")]
    public bool IsVerifyLot { get; set; }

    [JsonPropertyName("data")]
    public List<TraceData> Data { get; set; } = new();
}

/// <summary>
/// 追溯數據
/// </summary>
public class TraceData
{
    [JsonPropertyName("procName")]
    public string ProcName { get; set; } = string.Empty;

    [JsonPropertyName("devName")]
    public string DevName { get; set; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("workClass")]
    public string WorkClass { get; set; } = string.Empty;

    [JsonPropertyName("traceCode")]
    public string? TraceCode { get; set; }

    [JsonPropertyName("lotNo")]
    public string? LotNo { get; set; }

    [JsonPropertyName("partNumber")]
    public string? PartNumber { get; set; }

    [JsonPropertyName("remark")]
    public string? Remark { get; set; }

    [JsonPropertyName("paramData")]
    public List<ParamData>? ParamData { get; set; }

    [JsonPropertyName("benchmarks")]
    public List<ParamData>? Benchmarks { get; set; }

    [JsonPropertyName("otherData")]
    public List<ParamData>? OtherData { get; set; }
}

/// <summary>
/// 參數數據
/// </summary>
public class ParamData
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }
}

#endregion

#region 3. 追溯碼校驗 API Models

/// <summary>
/// 追溯碼校驗請求
/// </summary>
public class CheckCodeRequest
{
    [JsonPropertyName("woType")]
    public int WoType { get; set; } = 1;

    [JsonPropertyName("woNum")]
    public string WoNum { get; set; } = string.Empty;

    [JsonPropertyName("codes")]
    public List<string> Codes { get; set; } = new();

    [JsonPropertyName("PrtMacNo")]
    public string PrtMacNo { get; set; } = string.Empty;
}

#endregion

#region 通用 API 響應模型

/// <summary>
/// MES API 通用響應格式
/// </summary>
/// <typeparam name="T">data 字段的數據類型</typeparam>
public class MesApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
}

#endregion
