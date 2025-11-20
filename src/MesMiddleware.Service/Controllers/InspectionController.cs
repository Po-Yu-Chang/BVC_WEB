using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MesMiddleware.Service.Services.Queue;
using MesMiddleware.Shared.Models;
using System.Threading.Channels;

namespace MesMiddleware.Service.Controllers;

/// <summary>
/// API endpoint for equipment to submit inspection data.
/// Replaces shared memory IPC with HTTP REST API.
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
    /// Submit inspection data from equipment.
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

            _logger.LogInformation("Inspection data accepted: {TraceCodeOrLot} (RowNo: {RowNo})",
                data.TraceCode ?? data.LotNo,
                data.RowNo);

            return Accepted(new
            {
                message = "Inspection data accepted for processing",
                traceCode = data.TraceCode ?? data.LotNo,
                rowNo = data.RowNo
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
