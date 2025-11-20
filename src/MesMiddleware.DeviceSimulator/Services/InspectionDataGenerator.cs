using MesMiddleware.Shared.Models;

namespace MesMiddleware.DeviceSimulator.Services;

/// <summary>
/// 檢測數據生成器 - 使用演算法生成真實感的測試數據
/// </summary>
public class InspectionDataGenerator
{
    private static readonly string[] Operators = { "操作員A", "操作員B", "操作員C", "操作員D" };
    private static readonly string[] MachineTypes = { "鑽孔機", "銑床", "車床", "磨床" };
    private static readonly string[] ProcessNames = { "鑽孔", "銑削", "車削", "磨削", "檢測" };

    private int _sequenceCounter = 1;
    private readonly Random _random = new();

    /// <summary>
    /// 生成單筆檢測記錄
    /// </summary>
    public InspectionRecord GenerateSingle(string? customTraceCode = null, string? customLotNo = null, string? customResult = null)
    {
        var now = DateTime.Now;
        var date = now.ToString("yyyyMMdd");
        var time = now.ToString("HHmmss");

        // 生成 TraceCode (格式: TRACE-日期-流水號)
        var traceCode = customTraceCode ?? $"TRACE-{date}-{_sequenceCounter:D4}";

        // 生成 LotNo (格式: LOT-年月-批次號)
        var lotNo = customLotNo ?? $"LOT-{now:yyyyMM}-{_random.Next(1, 100):D3}";

        // 生成檢測結果 (80% 良品率)
        var result = customResult ?? (_random.Next(100) < 80 ? "OK" : "NG");

        _sequenceCounter++;

        // 生成測試數據
        var paramData = new List<ParamDataItem>
        {
            new ParamDataItem { Name = "孔徑", Value = $"{_random.NextDouble() * 10:F2}" },
            new ParamDataItem { Name = "深度", Value = $"{_random.NextDouble() * 5:F2}" },
            new ParamDataItem { Name = "位置誤差", Value = $"{_random.NextDouble() * 0.1:F3}" }
        };

        var benchmarks = new List<BenchmarkItem>
        {
            new BenchmarkItem { Name = "孔徑", UpperLimit = "10.0", LowerLimit = "9.5" },
            new BenchmarkItem { Name = "深度", UpperLimit = "5.0", LowerLimit = "4.5" },
            new BenchmarkItem { Name = "位置誤差", UpperLimit = "0.1", LowerLimit = null }
        };

        var otherData = new List<OtherDataItem>
        {
            new OtherDataItem { Key = "檢測結果", Value = result },
            new OtherDataItem { Key = "工單號", Value = $"WO-{now:yyyyMM}-{_random.Next(1, 100):D3}" },
            new OtherDataItem { Key = "備註", Value = result == "NG" ? GenerateNgRemark() : "正常" }
        };

        return new InspectionRecord
        {
            RowNo = _sequenceCounter.ToString(),
            TraceCode = traceCode,
            LotNo = lotNo,
            ProcName = ProcessNames[_random.Next(ProcessNames.Length)],
            DevName = $"{MachineTypes[_random.Next(MachineTypes.Length)]}-{_random.Next(1, 6):D2}",
            UserName = Operators[_random.Next(Operators.Length)],
            WorkClass = now.Hour < 18 ? "日班" : "夜班",
            ParamData = paramData,
            Benchmarks = benchmarks,
            OtherData = otherData,
            InspectionTime = now
        };
    }

    /// <summary>
    /// 批量生成檢測記錄
    /// </summary>
    public List<InspectionRecord> GenerateBatch(int count)
    {
        var records = new List<InspectionRecord>();
        for (int i = 0; i < count; i++)
        {
            records.Add(GenerateSingle());
            // 模擬時間間隔
            Thread.Sleep(1);
        }
        return records;
    }

    /// <summary>
    /// 生成隨機 TraceCode
    /// </summary>
    public string GenerateRandomTraceCode()
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        var seq = _random.Next(1, 9999);
        return $"TRACE-{date}-{seq:D4}";
    }

    /// <summary>
    /// 生成隨機 LotNo
    /// </summary>
    public string GenerateRandomLotNo()
    {
        var yearMonth = DateTime.Now.ToString("yyyyMM");
        var batch = _random.Next(1, 100);
        return $"LOT-{yearMonth}-{batch:D3}";
    }

    /// <summary>
    /// 生成隨機檢測結果
    /// </summary>
    public string GenerateRandomResult()
    {
        // 80% OK, 20% NG
        return _random.Next(100) < 80 ? "OK" : "NG";
    }

    /// <summary>
    /// 生成 NG 原因描述
    /// </summary>
    private string GenerateNgRemark()
    {
        var ngReasons = new[]
        {
            "尺寸超差",
            "表面刮傷",
            "孔位偏移",
            "毛邊過大",
            "材料異常",
            "設備故障"
        };
        return ngReasons[_random.Next(ngReasons.Length)];
    }

    /// <summary>
    /// 重置流水號計數器
    /// </summary>
    public void ResetSequence()
    {
        _sequenceCounter = 1;
    }
}
