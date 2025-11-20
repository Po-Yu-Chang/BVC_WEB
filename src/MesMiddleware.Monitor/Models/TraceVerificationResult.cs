namespace MesMiddleware.Monitor.Models;

/// <summary>
/// 追溯碼校驗結果
/// </summary>
public class TraceVerificationResult
{
    /// <summary>
    /// 驗證是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 返回訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 返回碼 (200=成功，其他=失敗)
    /// </summary>
    public string Code { get; set; } = string.Empty;
}
