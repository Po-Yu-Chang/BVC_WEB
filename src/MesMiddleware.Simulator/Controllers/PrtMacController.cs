using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MesMiddleware.Simulator.Data;
using MesMiddleware.Simulator.Models;
using MesMiddleware.Simulator.Services;

namespace MesMiddleware.Simulator.Controllers;

/// <summary>
/// 設備登錄 API Controller
/// </summary>
[ApiController]
[Route("CimforceTraceMgrDev/api/prtmac")]
public class PrtMacController : ControllerBase
{
    private readonly SimulatorDbContext _dbContext;
    private readonly TokenService _tokenService;
    private readonly SimulationConfigService _configService;
    private readonly ILogger<PrtMacController> _logger;

    public PrtMacController(
        SimulatorDbContext dbContext,
        TokenService tokenService,
        SimulationConfigService configService,
        ILogger<PrtMacController> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 設備登錄驗證並獲取 TOKEN
    /// POST /CimforceTraceMgrDev/api/prtmac/prtmacuserlogin
    /// </summary>
    [HttpPost("prtmacuserlogin")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            // 應用延遲
            await _configService.ApplyDelayAsync();

            // 獲取客戶端 IP (從 Header "Referrer" 或實際 IP)
            var clientIp = Request.Headers["Referrer"].FirstOrDefault()
                           ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                           ?? "Unknown";

            _logger.LogInformation("設備登錄請求: PrtMacNo={PrtMacNo}, IP={ClientIp}",
                request.PrtMacNo, clientIp);

            // 模擬失敗場景
            if (_configService.ShouldSimulateFailure())
            {
                return Ok(new MesApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Msg = $"本機台({request.PrtMacNo})IP({clientIp})與註冊電腦 IP 不匹配!",
                    Code = "300"
                });
            }

            // 查找或創建設備登錄記錄
            var existingLogin = await _dbContext.DeviceLogins
                .FirstOrDefaultAsync(d => d.PrtMacNo == request.PrtMacNo && d.IsActive);

            if (existingLogin != null && existingLogin.IpAddr != clientIp)
            {
                // IP 地址變更,需要重新登錄
                return Ok(new MesApiResponse<object>
                {
                    Success = false,
                    Data = null,
                    Msg = $"本機台({request.PrtMacNo})IP({clientIp})與註冊電腦 IP 不匹配!",
                    Code = "300"
                });
            }

            string token;
            if (existingLogin != null)
            {
                // 更新最後使用時間
                existingLogin.LastUsedTime = DateTime.Now;
                token = existingLogin.Token;
            }
            else
            {
                // 創建新的登錄記錄
                token = _tokenService.GenerateToken();
                var newLogin = new DeviceLogin
                {
                    PrtMacNo = request.PrtMacNo,
                    IpAddr = clientIp,
                    Token = token,
                    LoginTime = DateTime.Now,
                    LastUsedTime = DateTime.Now,
                    IsActive = true
                };
                _dbContext.DeviceLogins.Add(newLogin);
            }

            await _dbContext.SaveChangesAsync();

            // 返回成功響應
            var responseData = new LoginResponseData
            {
                PrtMacNo = request.PrtMacNo,
                IpAddr = clientIp,
                Token = token,
                SysUserId = null
            };

            return Ok(new MesApiResponse<LoginResponseData>
            {
                Success = true,
                Data = responseData,
                Msg = "success",
                Code = "200"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "設備登錄失敗");
            return Ok(new MesApiResponse<object>
            {
                Success = false,
                Data = null,
                Msg = $"系統錯誤: {ex.Message}",
                Code = "500"
            });
        }
    }
}
