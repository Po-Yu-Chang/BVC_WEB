using MesMiddleware.Shared.Models;
using MesMiddleware.Shared.Models.LabView;
using MesMiddleware.Shared.Models.MesApi;
using System.Globalization;

namespace MesMiddleware.Service.Services.Converters;

/// <summary>
/// Converts LabVIEW inspection data format to internal and MES API formats.
/// </summary>
public interface ILabViewDataConverter
{
    /// <summary>
    /// Convert LabVIEW request to internal InspectionRecord list
    /// </summary>
    List<InspectionRecord> ToInspectionRecords(LabViewInspectionRequest request);

    /// <summary>
    /// Convert internal InspectionRecord to MES API format
    /// </summary>
    MesTraceDataRequest ToMesTraceDataRequest(IEnumerable<InspectionRecord> records);

    /// <summary>
    /// Direct conversion from LabVIEW format to MES API format (pass-through with validation)
    /// </summary>
    MesTraceDataRequest LabViewToMesApi(LabViewInspectionRequest request);
}

/// <summary>
/// Implementation of LabVIEW data converter.
/// </summary>
public class LabViewDataConverter : ILabViewDataConverter
{
    private readonly ILogger<LabViewDataConverter> _logger;

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

            // Convert Benchmarks
            foreach (var benchmark in data.Benchmarks)
            {
                record.Benchmarks.Add(new BenchmarkItem
                {
                    Code = benchmark.Code,
                    Name = benchmark.Name ?? benchmark.Code ?? string.Empty,
                    Value = benchmark.Value,
                    Unit = benchmark.Unit,
                    Desc = benchmark.Desc
                });
            }

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

    /// <inheritdoc />
    public MesTraceDataRequest ToMesTraceDataRequest(IEnumerable<InspectionRecord> records)
    {
        var request = new MesTraceDataRequest
        {
            IsVerifyLot = false,
            Data = new List<MesTraceData>()
        };

        foreach (var record in records)
        {
            var traceData = new MesTraceData
            {
                DevName = record.DevName,
                ProcName = record.ProcName,
                UserName = record.UserName,
                WorkClass = record.WorkClass,
                TraceCode = record.TraceCode ?? string.Empty,
                LotNo = record.LotNo ?? string.Empty,
                PartNumber = record.PartNumber ?? string.Empty,
                Remark = record.Remark ?? string.Empty
            };

            // Convert ParamData
            foreach (var param in record.ParamData)
            {
                traceData.ParamData.Add(new MesParamDataItem
                {
                    Code = param.Code ?? param.Name,
                    Name = param.Name,
                    Value = param.Value,
                    Unit = param.Unit ?? string.Empty,
                    Desc = param.Desc ?? param.Name
                });
            }

            // Convert Benchmarks
            foreach (var benchmark in record.Benchmarks)
            {
                traceData.Benchmarks.Add(new MesBenchmarkItem
                {
                    Code = benchmark.Code ?? benchmark.Name,
                    Name = benchmark.Name,
                    Value = benchmark.Value ?? string.Empty,
                    Unit = benchmark.Unit ?? string.Empty,
                    Desc = benchmark.Desc ?? benchmark.Name
                });
            }

            // Convert OtherData
            foreach (var other in record.OtherData)
            {
                traceData.OtherData.Add(new MesOtherDataItem
                {
                    Code = other.Code ?? other.Name,
                    Name = other.Name,
                    Value = other.Value,
                    Unit = other.Unit ?? string.Empty,
                    Desc = other.Desc ?? other.Name
                });
            }

            request.Data.Add(traceData);
        }

        return request;
    }

    /// <inheritdoc />
    public MesTraceDataRequest LabViewToMesApi(LabViewInspectionRequest request)
    {
        var mesRequest = new MesTraceDataRequest
        {
            IsVerifyLot = request.IsVerifyLot,
            Data = new List<MesTraceData>()
        };

        foreach (var data in request.Data)
        {
            var traceData = new MesTraceData
            {
                DevName = data.DevName ?? string.Empty,
                ProcName = data.ProcName ?? string.Empty,
                UserName = data.UserName ?? string.Empty,
                WorkClass = data.WorkClass ?? string.Empty,
                TraceCode = data.TraceCode ?? string.Empty,
                LotNo = data.LotNo ?? string.Empty,
                PartNumber = data.PartNumber ?? string.Empty,
                Remark = data.Remark ?? string.Empty
            };

            // Separate paramData items: standard params vs defect quantities
            var standardParams = new List<LabViewParamDataItem>();
            var defectParams = new List<LabViewParamDataItem>();

            foreach (var param in data.ParamData)
            {
                // Defect_Qty_XX items should go to benchmarks per MES spec
                if (param.Code?.StartsWith("Defect_Qty_", StringComparison.OrdinalIgnoreCase) == true)
                {
                    defectParams.Add(param);
                }
                else
                {
                    standardParams.Add(param);
                }
            }

            // Add standard params to paramData
            foreach (var param in standardParams)
            {
                traceData.ParamData.Add(new MesParamDataItem
                {
                    Code = param.Code ?? string.Empty,
                    Name = param.Name ?? string.Empty,
                    Value = param.Value ?? string.Empty,
                    Unit = param.Unit ?? string.Empty,
                    Desc = param.Desc ?? string.Empty
                });
            }

            // Move defect quantities to benchmarks (as per MES spec)
            foreach (var defect in defectParams)
            {
                traceData.Benchmarks.Add(new MesBenchmarkItem
                {
                    Code = defect.Code ?? string.Empty,
                    Name = defect.Name ?? string.Empty,
                    Value = defect.Value ?? string.Empty,
                    Unit = defect.Unit ?? string.Empty,
                    Desc = defect.Desc ?? string.Empty
                });
            }

            // Add original benchmarks
            foreach (var benchmark in data.Benchmarks)
            {
                traceData.Benchmarks.Add(new MesBenchmarkItem
                {
                    Code = benchmark.Code ?? string.Empty,
                    Name = benchmark.Name ?? string.Empty,
                    Value = benchmark.Value ?? string.Empty,
                    Unit = benchmark.Unit ?? string.Empty,
                    Desc = benchmark.Desc ?? string.Empty
                });
            }

            // Add otherData
            foreach (var other in data.OtherData)
            {
                traceData.OtherData.Add(new MesOtherDataItem
                {
                    Code = other.Code ?? string.Empty,
                    Name = other.Name ?? string.Empty,
                    Value = other.Value ?? string.Empty,
                    Unit = other.Unit ?? string.Empty,
                    Desc = other.Desc ?? string.Empty
                });
            }

            mesRequest.Data.Add(traceData);
        }

        _logger.LogDebug("Converted LabVIEW request to MES API format: {Count} records", mesRequest.Data.Count);
        return mesRequest;
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
