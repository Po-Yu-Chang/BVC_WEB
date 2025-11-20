using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MesMiddleware.Service.Services.WebApi;
using System.Text.Json.Serialization;

namespace MesMiddleware.Service.Controllers;

/// <summary>
/// 追溯碼校驗 API endpoint (轉發請求到 MES Cloud)
/// 用於 Monitor 應用程式呼叫，驗證追溯碼是否屬於指定工單
/// </summary>
[ApiController]
[Route("api/trace")]
public class TraceVerificationController : ControllerBase
{
    private readonly IMesWebApiClient _webApiClient;
    private readonly ILogger<TraceVerificationController> _logger;

    public TraceVerificationController(
        IMesWebApiClient webApiClient,
        ILogger<TraceVerificationController> logger)
    {
        _webApiClient = webApiClient;
        _logger = logger;
    }

    /// <summary>
    /// 驗證追溯碼是否屬於指定工單 (轉發到 MES Cloud)
    /// Endpoint: POST /api/trace/verify
    /// </summary>
    /// <param name="request">驗證請求</param>
    /// <returns>驗證結果</returns>
    [HttpPost("verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerifyTraceCodes([FromBody] TraceVerificationRequest request)
    {
        try
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(request.WoNum))
            {
                return BadRequest(new
                {
                    success = false,
                    data = (object?)null,
                    msg = "工單號不能為空",
                    code = "-1"
                });
            }

            if (request.Codes == null || request.Codes.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    data = (object?)null,
                    msg = "追溯碼列表不能為空",
                    code = "-1"
                });
            }

            _logger.LogInformation("Verifying trace codes for work order {WorkOrder}, codes count: {Count}",
                request.WoNum, request.Codes.Count);

            // 呼叫 MES Cloud API
            var result = await _webApiClient.VerifyTraceCodesAsync(request);

            _logger.LogInformation("Trace verification result: Success={Success}, Code={Code}, Message={Message}",
                result.Success, result.Code, result.Msg);

            return Ok(new
            {
                success = result.Success,
                data = result.Data,
                msg = result.Msg,
                code = result.Code
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify trace codes for work order {WorkOrder}", request?.WoNum);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                data = (object?)null,
                msg = $"追溯碼校驗失敗: {ex.Message}",
                code = "500"
            });
        }
    }
}

/// <summary>
/// 追溯碼校驗請求 DTO
/// </summary>
public class TraceVerificationRequest
{
    /// <summary>
    /// 驗證類型 (固定傳 1，根據 PDF 文檔)
    /// </summary>
    [JsonPropertyName("woType")]
    public int WoType { get; set; } = 1;

    /// <summary>
    /// 工單編號
    /// </summary>
    [JsonPropertyName("woNum")]
    public string WoNum { get; set; } = string.Empty;

    /// <summary>
    /// 待驗證編碼列表
    /// </summary>
    [JsonPropertyName("codes")]
    public List<string> Codes { get; set; } = new();

    /// <summary>
    /// 設備編號 (Monitor 固定傳 "MONITOR")
    /// </summary>
    [JsonPropertyName("prtMacNo")]
    public string PrtMacNo { get; set; } = "MONITOR";
}
