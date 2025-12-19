using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MesMiddleware.Shared.Models;
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
    private readonly Channel<InspectionRecord> _inspectionChannel;
    private readonly ILogger<InspectionController> _logger;

    public InspectionController(
        IValidator<InspectionRecord> validator,
        Channel<InspectionRecord> inspectionChannel,
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

            // Write to in-memory channel (fast, non-blocking)
            await _inspectionChannel.Writer.WriteAsync(data);

            // 記錄到 Device → Monitor 歷史
            StatusController.AddDeviceToMonitorHistory(
                data.TraceCode ?? data.LotNo ?? "N/A",
                "Received",
                null
            );

            // 更新設備活動時間（IncrementReceived is called in InspectionChannelProcessor）
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
}
