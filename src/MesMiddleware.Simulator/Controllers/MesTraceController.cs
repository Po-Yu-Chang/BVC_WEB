using Microsoft.AspNetCore.Mvc;
using MesMiddleware.Simulator.Data;
using MesMiddleware.Simulator.Models;
using MesMiddleware.Simulator.Services;
using System.Text.Json;

namespace MesMiddleware.Simulator.Controllers;

/// <summary>
/// 追溯數據上傳 API Controller
/// </summary>
[ApiController]
[Route("CimforceTraceMgrDev/api/v1/MesTrace/TraceData")]
public class MesTraceController : ControllerBase
{
    private readonly SimulatorDbContext _dbContext;
    private readonly TokenService _tokenService;
    private readonly SimulationConfigService _configService;
    private readonly ILogger<MesTraceController> _logger;

    public MesTraceController(
        SimulatorDbContext dbContext,
        TokenService tokenService,
        SimulationConfigService configService,
        ILogger<MesTraceController> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 設備主動上傳追溯數據
    /// POST /CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3
    /// </summary>
    [HttpPost("AddData3")]
    public async Task<IActionResult> AddData3([FromBody] TraceDataRequest request)
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

            // 模擬失敗場景
            if (_configService.ShouldSimulateFailure())
            {
                return Ok(new MesApiResponse<object>
                {
                    Success = false,
                    Data = "",
                    Msg = "工單[202401-01-000]不存在",
                    Code = "-1"
                });
            }

            // 保存追溯數據記錄
            foreach (var data in request.Data)
            {
                var record = new TraceDataRecord
                {
                    UploadTime = DateTime.Now,
                    TraceCode = data.TraceCode,
                    LotNo = data.LotNo,
                    ProcName = data.ProcName,
                    DevName = data.DevName,
                    UserName = data.UserName,
                    PartNumber = data.PartNumber,
                    FullJsonData = JsonSerializer.Serialize(data),
                    IsVerifyLot = request.IsVerifyLot
                };
                _dbContext.TraceDataRecords.Add(record);
            }

            await _dbContext.SaveChangesAsync();

            // 更新 Token 最後使用時間
            await _tokenService.UpdateTokenLastUsedAsync(token);

            _logger.LogInformation("數據上傳成功: DevName={DevName}, 記錄數={Count}",
                request.Data.FirstOrDefault()?.DevName, request.Data.Count);

            // 返回成功響應
            return Ok(new MesApiResponse<object>
            {
                Success = true,
                Data = "",
                Msg = "上傳成功",
                Code = "200"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "數據上傳失敗");
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
