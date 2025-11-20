namespace MesMiddleware.Simulator.Services;

/// <summary>
/// 模擬行為配置服務
/// </summary>
public class SimulationConfigService
{
    /// <summary>
    /// 模擬模式
    /// </summary>
    public SimulationMode Mode { get; set; } = SimulationMode.Normal;

    /// <summary>
    /// 延遲時間 (毫秒)
    /// </summary>
    public int DelayMs { get; set; } = 0;

    /// <summary>
    /// 隨機故障率 (0-100)
    /// </summary>
    public int FailureRate { get; set; } = 0;

    /// <summary>
    /// 是否應該模擬失敗
    /// </summary>
    public bool ShouldSimulateFailure()
    {
        if (Mode == SimulationMode.AlwaysFail)
            return true;

        if (Mode == SimulationMode.Random && FailureRate > 0)
        {
            return Random.Shared.Next(100) < FailureRate;
        }

        return false;
    }

    /// <summary>
    /// 應用延遲
    /// </summary>
    public async Task ApplyDelayAsync()
    {
        if (DelayMs > 0)
        {
            await Task.Delay(DelayMs);
        }
    }
}

/// <summary>
/// 模擬模式枚舉
/// </summary>
public enum SimulationMode
{
    /// <summary>
    /// 正常響應
    /// </summary>
    Normal,

    /// <summary>
    /// 總是失敗
    /// </summary>
    AlwaysFail,

    /// <summary>
    /// 隨機故障
    /// </summary>
    Random,

    /// <summary>
    /// 延遲響應
    /// </summary>
    Delayed
}
