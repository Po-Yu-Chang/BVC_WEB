using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MesMiddleware.Service.Services.Converters;
using MesMiddleware.Shared.Models;
using MesMiddleware.Shared.Models.LabView;
using System.Threading.Channels;

namespace MesMiddleware.Service.Controllers;

/// <summary>
/// API endpoint for LabVIEW equipment to submit inspection data.
/// Accepts LabVIEW JSON format and converts to internal format.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LabViewController : ControllerBase
{
    private readonly ILabViewDataConverter _converter;
    private readonly Channel<InspectionRecord> _inspectionChannel;
    private readonly ILogger<LabViewController> _logger;

    public LabViewController(
        ILabViewDataConverter converter,
        Channel<InspectionRecord> inspectionChannel,
        ILogger<LabViewController> logger)
    {
        _converter = converter;
        _inspectionChannel = inspectionChannel;
        _logger = logger;
    }

    /// <summary>
    /// Submit inspection data from LabVIEW equipment.
    /// Endpoint: POST /api/labview/submit
    /// Accepts: {"isVerifyLot": false, "data": [...]}
    /// </summary>
    /// <param name="request">LabVIEW inspection request JSON</param>
    /// <returns>202 Accepted if data queued successfully</returns>
    [HttpPost("submit")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitLabViewData([FromBody] LabViewInspectionRequest request)
    {
        try
        {
            // Validate request structure
            if (request.Data == null || request.Data.Count == 0)
            {
                _logger.LogWarning("LabVIEW request rejected: no data provided");
                return BadRequest(new
                {
                    success = false,
                    code = "400",
                    msg = "No inspection data provided in request",
                    data = (object?)null
                });
            }

            // Validate required fields for each data item
            var errors = ValidateLabViewData(request);
            if (errors.Any())
            {
                _logger.LogWarning("LabVIEW validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(new
                {
                    success = false,
                    code = "400",
                    msg = string.Join("; ", errors),
                    data = (object?)null
                });
            }

            // Convert to internal format
            var records = _converter.ToInspectionRecords(request);

            // Write all records to channel
            foreach (var record in records)
            {
                await _inspectionChannel.Writer.WriteAsync(record);
                _logger.LogInformation("LabVIEW data accepted: {TraceCodeOrLot} (DevName: {DevName})",
                    record.TraceCode ?? record.LotNo,
                    record.DevName);
            }

            // Return MES-compatible success response
            return Accepted(new
            {
                success = true,
                code = "200",
                msg = $"上传成功 (Accepted {records.Count} records)",
                data = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process LabVIEW inspection data");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                code = "500",
                msg = "Internal server error: " + ex.Message,
                data = (object?)null
            });
        }
    }

    /// <summary>
    /// Validate LabVIEW inspection data
    /// </summary>
    private List<string> ValidateLabViewData(LabViewInspectionRequest request)
    {
        var errors = new List<string>();

        for (int i = 0; i < request.Data.Count; i++)
        {
            var data = request.Data[i];
            var prefix = request.Data.Count > 1 ? $"[{i}] " : "";

            if (string.IsNullOrWhiteSpace(data.ProcName))
                errors.Add($"{prefix}procName is required");

            if (string.IsNullOrWhiteSpace(data.DevName))
                errors.Add($"{prefix}devName is required");

            if (string.IsNullOrWhiteSpace(data.UserName))
                errors.Add($"{prefix}userName is required");

            if (string.IsNullOrWhiteSpace(data.WorkClass))
                errors.Add($"{prefix}workClass is required");

            if (string.IsNullOrWhiteSpace(data.TraceCode) && string.IsNullOrWhiteSpace(data.LotNo))
                errors.Add($"{prefix}Either traceCode or lotNo must be provided");

            // Validate paramData items have values
            foreach (var param in data.ParamData)
            {
                if (string.IsNullOrWhiteSpace(param.Code) && string.IsNullOrWhiteSpace(param.Name))
                    errors.Add($"{prefix}paramData item missing code/name");
            }

            // Check for CheckTime in otherData
            var hasCheckTime = data.OtherData.Any(x =>
                x.Code?.Equals("CheckTime", StringComparison.OrdinalIgnoreCase) == true ||
                x.Name?.Equals("CheckTime", StringComparison.OrdinalIgnoreCase) == true);

            if (!hasCheckTime)
            {
                _logger.LogWarning("{Prefix}otherData missing CheckTime, will use current time", prefix);
            }
        }

        return errors;
    }
}
