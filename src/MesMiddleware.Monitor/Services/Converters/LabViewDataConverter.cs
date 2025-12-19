using MesMiddleware.Shared.Models;
using MesMiddleware.Shared.Models.LabView;
using Microsoft.Extensions.Logging;
using System.Globalization;

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
                    Key = other.Name ?? other.Code ?? string.Empty,
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
