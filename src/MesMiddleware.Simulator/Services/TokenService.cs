using Microsoft.EntityFrameworkCore;
using MesMiddleware.Simulator.Data;
using MesMiddleware.Simulator.Models;

namespace MesMiddleware.Simulator.Services;

/// <summary>
/// Token 生成與驗證服務
/// </summary>
public class TokenService
{
    private readonly SimulatorDbContext _dbContext;

    public TokenService(SimulatorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 生成 Token (格式: autoprt + 32位隨機字串)
    /// </summary>
    public string GenerateToken()
    {
        var guid = Guid.NewGuid().ToString("N"); // 32位無連字符
        return $"autoprt{guid}";
    }

    /// <summary>
    /// 驗證 Token 是否有效
    /// </summary>
    public async Task<DeviceLogin?> ValidateTokenAsync(string token)
    {
        return await _dbContext.DeviceLogins
            .FirstOrDefaultAsync(d => d.Token == token && d.IsActive);
    }

    /// <summary>
    /// 更新 Token 最後使用時間
    /// </summary>
    public async Task UpdateTokenLastUsedAsync(string token)
    {
        var deviceLogin = await _dbContext.DeviceLogins
            .FirstOrDefaultAsync(d => d.Token == token);

        if (deviceLogin != null)
        {
            deviceLogin.LastUsedTime = DateTime.Now;
            await _dbContext.SaveChangesAsync();
        }
    }
}
