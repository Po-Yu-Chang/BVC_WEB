using MesMiddleware.Shared.Models;
using MesMiddleware.Shared.Models.LabView;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MesMiddleware.Monitor.Services.Converters;

/// <summary>
/// Converts LabVIEW inspection data format to internal InspectionRecord format.
/// </summary>
public interface ILabViewDataConverter
{
    /// <summary>
    /// Convert LabVIEW request to internal InspectionRecord list
    /// </summary>
    List<InspectionRecord> ToInspectionRecords(LabViewInspectionRequest request);
}

/// <summary>
/// Implementation of LabVIEW data converter.
/// Handles LabVIEW naming conventions for limits:
/// - *_UpperLimit → UpperLimit field
/// - *_LowerLimit, *_LpperLimit → LowerLimit field
/// - *_Target → Target field
/// - *_Tolerance → Tolerance field
/// </summary>
public class LabViewDataConverter : ILabViewDataConverter
{
    private readonly ILogger<LabViewDataConverter> _logger;

    // 限值後綴模式 (支援 UpperLimit, LowerLimit, LpperLimit 拼寫錯誤)
    private static readonly Regex UpperLimitPattern = new(@"^(.+?)_(?:Upper_?Limit|UpperLimit)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LowerLimitPattern = new(@"^(.+?)_(?:Lower_?Limit|LowerLimit|Lpper_?Limit|LpperLimit)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TargetPattern = new(@"^(.+?)_Target$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TolerancePattern = new(@"^(.+?)_Tolerance$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public LabViewDataConverter(ILogger<LabViewDataConverter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public List<InspectionRecord> ToInspectionRecords(LabViewInspectionRequest request)
    {
        var records = new List<InspectionRecord>();

        foreach (var data in request.Data)
        {
            var record = new InspectionRecord
            {
                ProcName = data.ProcName ?? string.Empty,
                DevName = data.DevName ?? string.Empty,
                UserName = data.UserName ?? string.Empty,
                WorkClass = data.WorkClass ?? string.Empty,
                TraceCode = data.TraceCode,
                LotNo = data.LotNo,
                PartNumber = data.PartNumber,
                Remark = data.Remark,
                InspectionTime = ExtractCheckTime(data.OtherData)
            };

            // Convert ParamData
            foreach (var param in data.ParamData)
            {
                record.ParamData.Add(new ParamDataItem
                {
                    Code = param.Code,
                    Name = param.Name ?? param.Code ?? string.Empty,
                    Value = param.Value ?? string.Empty,
                    Unit = param.Unit,
                    Desc = param.Desc
                });
            }

            // Convert Benchmarks with smart limit parsing
            record.Benchmarks.AddRange(ConvertBenchmarksWithLimits(data.Benchmarks));

            // Convert OtherData
            foreach (var other in data.OtherData)
            {
                record.OtherData.Add(new OtherDataItem
                {
                    Code = other.Code,
                    Name = other.Name ?? other.Code ?? string.Empty,
                    Value = other.Value ?? string.Empty,
                    Unit = other.Unit,
                    Desc = other.Desc
                });
            }

            records.Add(record);
        }

        _logger.LogDebug("Converted {Count} LabVIEW records to InspectionRecords", records.Count);
        return records;
    }

    /// <summary>
    /// 智能轉換 Benchmarks，解析 LabVIEW 命名慣例中的限值
    ///
    /// LabVIEW 格式範例:
    /// - {"name":"Ubore_Diameter_UpperLimit","value":"120"} → UpperLimit = "120"
    /// - {"name":"Ubore_Diameter_LowerLimit","value":"90"} → LowerLimit = "90"
    /// - {"name":"Ubore_Diameter_X","value":"107.147"} → 量測值，查找對應限值
    ///
    /// 轉換策略:
    /// 1. 第一遍: 建立限值查詢表 (base parameter name → limits)
    /// 2. 第二遍: 轉換每個 benchmark，填入對應的限值
    /// </summary>
    private List<BenchmarkItem> ConvertBenchmarksWithLimits(List<LabViewBenchmarkItem> benchmarks)
    {
        var result = new List<BenchmarkItem>();

        // 第一遍: 建立限值查詢表
        var limitsLookup = new Dictionary<string, LimitValues>(StringComparer.OrdinalIgnoreCase);

        foreach (var benchmark in benchmarks)
        {
            var name = benchmark.Name ?? benchmark.Code ?? string.Empty;
            var value = benchmark.Value;

            // 檢查是否為限值定義
            var (baseName, limitType) = ParseLimitSuffix(name);
            if (baseName != null && limitType != null)
            {
                if (!limitsLookup.TryGetValue(baseName, out var limits))
                {
                    limits = new LimitValues();
                    limitsLookup[baseName] = limits;
                }

                switch (limitType)
                {
                    case "UpperLimit":
                        limits.UpperLimit = value;
                        limits.Unit ??= benchmark.Unit;
                        break;
                    case "LowerLimit":
                        limits.LowerLimit = value;
                        limits.Unit ??= benchmark.Unit;
                        break;
                    case "Target":
                        limits.Target = value;
                        limits.Unit ??= benchmark.Unit;
                        break;
                    case "Tolerance":
                        limits.Tolerance = value;
                        limits.Unit ??= benchmark.Unit;
                        break;
                }
            }
        }

        _logger.LogDebug("Built limits lookup with {Count} base parameters", limitsLookup.Count);

        // 第二遍: 轉換 benchmarks，填入限值
        foreach (var benchmark in benchmarks)
        {
            var name = benchmark.Name ?? benchmark.Code ?? string.Empty;

            // 檢查是否為限值定義項目
            var (baseName, limitType) = ParseLimitSuffix(name);

            if (baseName != null && limitType != null)
            {
                // 這是限值定義項目，轉換為帶有限值的 benchmark
                var item = new BenchmarkItem
                {
                    Code = benchmark.Code,
                    Name = baseName, // 使用基礎參數名稱
                    Value = benchmark.Value,
                    Unit = benchmark.Unit,
                    Desc = benchmark.Desc
                };

                // 根據限值類型設定對應欄位
                switch (limitType)
                {
                    case "UpperLimit":
                        item.UpperLimit = benchmark.Value;
                        // 同時查找對應的 LowerLimit
                        if (limitsLookup.TryGetValue(baseName, out var limitsU))
                        {
                            item.LowerLimit = limitsU.LowerLimit;
                            item.Target = limitsU.Target;
                            item.Tolerance = limitsU.Tolerance;
                        }
                        break;
                    case "LowerLimit":
                        item.LowerLimit = benchmark.Value;
                        // 同時查找對應的 UpperLimit
                        if (limitsLookup.TryGetValue(baseName, out var limitsL))
                        {
                            item.UpperLimit = limitsL.UpperLimit;
                            item.Target = limitsL.Target;
                            item.Tolerance = limitsL.Tolerance;
                        }
                        break;
                    case "Target":
                        item.Target = benchmark.Value;
                        if (limitsLookup.TryGetValue(baseName, out var limitsT))
                        {
                            item.UpperLimit = limitsT.UpperLimit;
                            item.LowerLimit = limitsT.LowerLimit;
                            item.Tolerance = limitsT.Tolerance;
                        }
                        break;
                    case "Tolerance":
                        item.Tolerance = benchmark.Value;
                        if (limitsLookup.TryGetValue(baseName, out var limitsTol))
                        {
                            item.UpperLimit = limitsTol.UpperLimit;
                            item.LowerLimit = limitsTol.LowerLimit;
                            item.Target = limitsTol.Target;
                        }
                        break;
                }

                result.Add(item);
            }
            else
            {
                // 這是一般 benchmark 項目 (量測值或設定項)
                var item = new BenchmarkItem
                {
                    Code = benchmark.Code,
                    Name = name,
                    Value = benchmark.Value,
                    Unit = benchmark.Unit,
                    Desc = benchmark.Desc
                };

                // 嘗試查找對應的限值 (移除數字後綴來匹配)
                var baseParamName = ExtractBaseParameterName(name);
                if (baseParamName != null && limitsLookup.TryGetValue(baseParamName, out var limits))
                {
                    item.UpperLimit = limits.UpperLimit;
                    item.LowerLimit = limits.LowerLimit;
                    item.Target = limits.Target;
                    item.Tolerance = limits.Tolerance;
                }

                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// 解析限值後綴，返回基礎參數名稱和限值類型
    /// </summary>
    private (string? baseName, string? limitType) ParseLimitSuffix(string name)
    {
        // 檢查 UpperLimit
        var match = UpperLimitPattern.Match(name);
        if (match.Success)
            return (match.Groups[1].Value, "UpperLimit");

        // 檢查 LowerLimit (包含 LpperLimit 拼寫錯誤)
        match = LowerLimitPattern.Match(name);
        if (match.Success)
            return (match.Groups[1].Value, "LowerLimit");

        // 檢查 Target
        match = TargetPattern.Match(name);
        if (match.Success)
            return (match.Groups[1].Value, "Target");

        // 檢查 Tolerance
        match = TolerancePattern.Match(name);
        if (match.Success)
            return (match.Groups[1].Value, "Tolerance");

        return (null, null);
    }

    /// <summary>
    /// 從量測值名稱中提取基礎參數名稱 (移除數字後綴)
    /// 例如: "Ubore_Diameter_X_01" → "Ubore_Diameter_X"
    ///       "Ubore_Diameter_X" → "Ubore_Diameter"
    /// </summary>
    private string? ExtractBaseParameterName(string name)
    {
        // 移除數字後綴 (如 _01, _02 等)
        var withoutNumericSuffix = Regex.Replace(name, @"_\d+$", "");

        // 嘗試移除 _X, _Y 等座標後綴
        var withoutCoordinateSuffix = Regex.Replace(withoutNumericSuffix, @"_[XYZ]$", "");

        // 如果有變化，返回處理後的名稱
        if (withoutCoordinateSuffix != name)
            return withoutCoordinateSuffix;

        if (withoutNumericSuffix != name)
            return withoutNumericSuffix;

        return null;
    }

    /// <summary>
    /// 限值暫存結構
    /// </summary>
    private class LimitValues
    {
        public string? UpperLimit { get; set; }
        public string? LowerLimit { get; set; }
        public string? Target { get; set; }
        public string? Tolerance { get; set; }
        public string? Unit { get; set; }
    }

    /// <summary>
    /// Extract CheckTime from OtherData and convert to DateTime
    /// </summary>
    private DateTime ExtractCheckTime(List<LabViewOtherDataItem> otherData)
    {
        var checkTimeItem = otherData.FirstOrDefault(x =>
            x.Code?.Equals("CheckTime", StringComparison.OrdinalIgnoreCase) == true ||
            x.Name?.Equals("CheckTime", StringComparison.OrdinalIgnoreCase) == true);

        if (checkTimeItem?.Value != null)
        {
            // Try parsing format: "yyyy-MM-dd HH:mm:ss"
            if (DateTime.TryParseExact(checkTimeItem.Value, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
            {
                return parsedTime;
            }

            // Try general parsing
            if (DateTime.TryParse(checkTimeItem.Value, out parsedTime))
            {
                return parsedTime;
            }
        }

        return DateTime.UtcNow;
    }
}
