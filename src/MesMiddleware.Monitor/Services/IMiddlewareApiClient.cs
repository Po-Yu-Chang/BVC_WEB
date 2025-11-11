using MesMiddleware.Monitor.Models;

namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 與 MES 中介軟體服務通訊的客戶端介面
/// 用於取得服務狀態、上傳歷史和佇列資訊
/// </summary>
public interface IMiddlewareApiClient
{
    /// <summary>
    /// 取得目前的連線狀態（Connected/Disconnected/Retrying）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>連線狀態資料傳輸物件</returns>
    Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得最近的上傳歷史（成功和失敗的上傳）
    /// </summary>
    /// <param name="maxRecords">最大記錄數量，預設為 1000</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上傳記錄清單</returns>
    Task<List<UploadRecord>> GetUploadHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得共享記憶體活動日誌（最近接收到的資料事件）
    /// </summary>
    /// <param name="maxRecords">最大記錄數量，預設為 100</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>共享記憶體活動清單</returns>
    Task<List<SharedMemoryActivity>> GetSharedMemoryActivityAsync(int maxRecords = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取得命令歷史（透過共享記憶體發送的設備命令）
    /// 屬於使用者故事 3（雙向命令與控制）的一部分
    /// </summary>
    /// <param name="maxRecords">最大記錄數量，預設為 1000</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命令記錄清單</returns>
    Task<List<CommandRecord>> GetCommandHistoryAsync(int maxRecords = 1000, CancellationToken cancellationToken = default);
}
