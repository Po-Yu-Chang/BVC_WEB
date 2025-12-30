using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MesMiddleware.Shared.Models;
using MesMiddleware.Shared.Models.LabView;
using System.Threading.Channels;

namespace MesMiddleware.Monitor.Controllers;

/// <summary>
/// API endpoint for DeviceSimulator to submit inspection data.
/// Monitor acts as middleware: Device → Monitor → MES Cloud
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InspectionController : ControllerBase
{
    private readonly IValidator<InspectionRecord> _validator;
    private readonly Channel<LabViewInspectionRequest> _inspectionChannel;
    private readonly ILogger<InspectionController> _logger;

    public InspectionController(
        IValidator<InspectionRecord> validator,
        Channel<LabViewInspectionRequest> inspectionChannel,
        ILogger<InspectionController> logger)
    {
        _validator = validator;
        _inspectionChannel = inspectionChannel;
        _logger = logger;
    }

    /// <summary>
    /// Submit inspection data from DeviceSimulator.
    /// Endpoint: POST /api/inspection/submit
    /// </summary>
    /// <param name="data">Inspection record JSON</param>
    /// <returns>202 Accepted if validation passes and data queued</returns>
    [HttpPost("submit")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitInspectionData([FromBody] InspectionRecord data)
    {
        try
        {
            // Validate incoming data
            var validationResult = await _validator.ValidateAsync(data);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for inspection data: {Errors}",
                    string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));

                return BadRequest(new
                {
                    errors = validationResult.Errors.Select(e => new
                    {
                        field = e.PropertyName,
                        message = e.ErrorMessage
                    })
                });
            }

            // 將 InspectionRecord 轉換為 LabViewInspectionRequest 格式
            var labViewRequest = ConvertToLabViewRequest(data);

            // Write to in-memory channel
            await _inspectionChannel.Writer.WriteAsync(labViewRequest);

            // 記錄到 Device → Monitor 歷史
            StatusController.AddDeviceToMonitorHistory(
                data.TraceCode ?? data.LotNo ?? "N/A",
                "Received",
                null
            );

            // 更新設備活動時間
            StatusController.UpdateDeviceActivity();

            _logger.LogInformation("Inspection data accepted: {TraceCodeOrLot}",
                data.TraceCode ?? data.LotNo);

            return Accepted(new
            {
                message = "Inspection data accepted for processing",
                traceCode = data.TraceCode ?? data.LotNo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept inspection data");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Internal server error"
            });
        }
    }

    /// <summary>
    /// 將 InspectionRecord 轉換為 LabViewInspectionRequest 格式
    /// </summary>
    private LabViewInspectionRequest ConvertToLabViewRequest(InspectionRecord data)
    {
        var labViewData = new LabViewInspectionData
        {
            ProcName = data.ProcName,
            DevName = data.DevName,
            UserName = data.UserName,
            WorkClass = data.WorkClass,
            TraceCode = data.TraceCode,
            LotNo = data.LotNo,
            PartNumber = data.PartNumber,
            Remark = data.Remark
        };

        // 轉換 ParamData
        foreach (var param in data.ParamData)
        {
            labViewData.ParamData.Add(new LabViewParamDataItem
            {
                Code = param.Code,
                Name = param.Name,
                Value = param.Value,
                Unit = param.Unit,
                Desc = param.Desc
            });
        }

        // 轉換 Benchmarks
        foreach (var benchmark in data.Benchmarks)
        {
            labViewData.Benchmarks.Add(new LabViewBenchmarkItem
            {
                Code = benchmark.Code,
                Name = benchmark.Name,
                Value = benchmark.Value,
                Unit = benchmark.Unit,
                Desc = benchmark.Desc
            });
        }

        // 轉換 OtherData
        foreach (var other in data.OtherData)
        {
            labViewData.OtherData.Add(new LabViewOtherDataItem
            {
                Code = other.Code,
                Name = other.Name,
                Value = other.Value,
                Unit = other.Unit,
                Desc = other.Desc
            });
        }

        return new LabViewInspectionRequest
        {
            IsVerifyLot = false,
            Data = new List<LabViewInspectionData> { labViewData }
        };
    }
}
