using MesMiddleware.Monitor.Models;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 與 MES 中介軟體服務通訊的客戶端介面
/// 用於取得服務狀態、上傳歷史和佇列資訊
/// </summary>
public interface IMiddlewareApiClient
{
    /// <summary>
    /// 取得目前的連線狀態(Connected/Disconnected/Retrying)
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>連線狀態資料傳輸物件</returns>
    Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得最近的上傳歷史 (設備 → 中介軟體的佇列歷史)
    /// </summary>
    /// <param name="maxRecords">最大記錄數量,預設為 1000</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上傳記錄清單</returns>
    Task<List<UploadRecord>> GetUploadHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得 MES Cloud 上傳歷史 (中介軟體 → MES Cloud 的上傳記錄)
    /// </summary>
    /// <param name="maxRecords">最大記錄數量,預設為 100</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上傳記錄清單</returns>
    Task<List<UploadRecord>> GetMesUploadHistoryAsync(int maxRecords = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手動重試失敗的上傳
    /// </summary>
    /// <param name="uploadId">上傳記錄 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重試是否成功</returns>
    Task<bool> RetryUploadAsync(Guid uploadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 驗證追溯碼是否屬於指定工單 (追溯碼校驗 - 混批檢測)
    /// </summary>
    /// <param name="workOrderNumber">工單號</param>
    /// <param name="traceCodes">追溯碼列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>驗證結果</returns>
    Task<TraceVerificationResult> VerifyTraceCodesAsync(string workOrderNumber, List<string> traceCodes, CancellationToken cancellationToken = default);
}
