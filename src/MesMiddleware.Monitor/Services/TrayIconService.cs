namespace MesMiddleware.Monitor.Services;

/// <summary>
/// 管理系統匣圖示和通知的服務
/// 處理圖示狀態變更（綠色/紅色/黃色）和氣球通知
/// </summary>
public class TrayIconService : IDisposable
{
    /// <summary>
    /// 目前的圖示狀態
    /// </summary>
    public string CurrentIconStatus { get; private set; } = "Disconnected";

    /// <summary>
    /// 圖示顏色
    /// </summary>
    public string IconColor { get; private set; } = "Red";

    /// <summary>
    /// 是否已最小化到系統匣
    /// </summary>
    public bool IsMinimizedToTray { get; private set; }

    /// <summary>
    /// 是否已釋放資源
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 氣球提示顯示事件
    /// </summary>
    public event EventHandler<BalloonTipEventArgs>? BalloonTipShown;

    /// <summary>
    /// 視窗可見性變更事件
    /// </summary>
    public event EventHandler<WindowVisibilityEventArgs>? WindowVisibilityChanged;

    /// <summary>
    /// 圖示狀態變更事件
    /// </summary>
    public event EventHandler<IconStatusEventArgs>? IconStatusChanged;

    /// <summary>
    /// 設定圖示狀態
    /// </summary>
    /// <param name="status">狀態字串</param>
    public void SetIconStatus(string status)
    {
        CurrentIconStatus = status;
        // 根據狀態設定圖示顏色
        IconColor = status switch
        {
            "Connected" => "Green",
            "Disconnected" => "Red",
            "Retrying" => "Yellow",
            _ => "Gray"
        };

        IconStatusChanged?.Invoke(this, new IconStatusEventArgs { Status = status });
    }

    /// <summary>
    /// 顯示氣球通知
    /// </summary>
    /// <param name="title">通知標題</param>
    /// <param name="message">通知訊息</param>
    public void ShowBalloonNotification(string title, string message)
    {
        BalloonTipShown?.Invoke(this, new BalloonTipEventArgs
        {
            Title = title,
            Message = message
        });
    }

    /// <summary>
    /// 最小化視窗到系統匣
    /// </summary>
    public void MinimizeWindowToTray()
    {
        IsMinimizedToTray = true;
        WindowVisibilityChanged?.Invoke(this, new WindowVisibilityEventArgs { IsVisible = false });
    }

    /// <summary>
    /// 從系統匣還原視窗
    /// </summary>
    public void RestoreWindowFromTray()
    {
        IsMinimizedToTray = false;
        WindowVisibilityChanged?.Invoke(this, new WindowVisibilityEventArgs { IsVisible = true });
    }

    /// <summary>
    /// 處理系統匣圖示點擊事件
    /// </summary>
    public void OnTrayIconClick()
    {
        if (IsMinimizedToTray)
        {
            RestoreWindowFromTray();
        }
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 氣球提示事件參數
/// </summary>
public class BalloonTipEventArgs : EventArgs
{
    /// <summary>
    /// 通知標題
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 通知訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 視窗可見性事件參數
/// </summary>
public class WindowVisibilityEventArgs : EventArgs
{
    /// <summary>
    /// 視窗是否可見
    /// </summary>
    public bool IsVisible { get; set; }
}

/// <summary>
/// 圖示狀態事件參數
/// </summary>
public class IconStatusEventArgs : EventArgs
{
    /// <summary>
    /// 狀態字串
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
