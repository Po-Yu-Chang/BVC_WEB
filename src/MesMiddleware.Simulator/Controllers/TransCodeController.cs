using Microsoft.AspNetCore.Mvc;
using MesMiddleware.Simulator.Data;
using MesMiddleware.Simulator.Models;
using MesMiddleware.Simulator.Services;

namespace MesMiddleware.Simulator.Controllers;

/// <summary>
/// 追溯碼校驗 API Controller
/// </summary>
[ApiController]
[Route("CimforceTraceMgrDev/api/transcode")]
public class TransCodeController : ControllerBase
{
    private readonly SimulatorDbContext _dbContext;
    private readonly TokenService _tokenService;
    private readonly SimulationConfigService _configService;
    private readonly ILogger<TransCodeController> _logger;

    public TransCodeController(
        SimulatorDbContext dbContext,
        TokenService tokenService,
        SimulationConfigService configService,
        ILogger<TransCodeController> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 驗證追溯碼與 LOT 號是否一致 (混批檢測)
    /// POST /CimforceTraceMgrDev/api/transcode/checkcode
    /// </summary>
    [HttpPost("checkcode")]
    public async Task<IActionResult> CheckCode([FromBody] CheckCodeRequest request)
    {
        try
        {
            // 驗證 accessToken
            var token = Request.Headers["accessToken"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                return Ok(new MesApiResponse<object>
                {
                    Success = false,
                    Data = "",
                    Msg = "缺少 accessToken",
                    Code = "401"
                });
            }

            var deviceLogin = await _tokenService.ValidateTokenAsync(token);
            if (deviceLogin == null)
            {
                return Ok(new MesApiResponse<object>
                {
                    Success = false,
                    Data = "",
                    Msg = "Token 無效或已過期",
                    Code = "401"
                });
            }

            // 應用延遲
            await _configService.ApplyDelayAsync();

            // 模擬失敗場景 - 混批錯誤
            if (_configService.ShouldSimulateFailure())
            {
                var failedCodes = string.Join("。", request.Codes.Select(c => $"編碼[{c}]不屬於工單"));
                return Ok(new MesApiResponse<object>
                {
                    Success = false,
                    Data = "",
                    Msg = $"驗證失敗:{failedCodes}。",
                    Code = "0"
                });
            }

            // 更新 Token 最後使用時間
            await _tokenService.UpdateTokenLastUsedAsync(token);

            _logger.LogInformation("追溯碼校驗: WoNum={WoNum}, 編碼數={Count}",
                request.WoNum, request.Codes.Count);

            // 返回成功響應
            return Ok(new MesApiResponse<object>
            {
                Success = true,
                Data = "",
                Msg = "驗證成功",
                Code = "200"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "追溯碼校驗失敗");
            return Ok(new MesApiResponse<object>
            {
                Success = false,
                Data = "",
                Msg = $"系統錯誤: {ex.Message}",
                Code = "500"
            });
        }
    }
}
