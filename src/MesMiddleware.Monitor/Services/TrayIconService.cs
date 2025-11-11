namespace MesMiddleware.Monitor.Services;

/// <summary>
/// Service for managing system tray icon and notifications.
/// Handles icon state changes (green/red/yellow) and balloon notifications.
/// </summary>
public class TrayIconService : IDisposable
{
    public string CurrentIconStatus { get; private set; } = "Disconnected";
    public string IconColor { get; private set; } = "Red";
    public bool IsMinimizedToTray { get; private set; }
    public bool IsDisposed { get; private set; }

    public event EventHandler<BalloonTipEventArgs>? BalloonTipShown;
    public event EventHandler<WindowVisibilityEventArgs>? WindowVisibilityChanged;
    public event EventHandler<IconStatusEventArgs>? IconStatusChanged;

    public void SetIconStatus(string status)
    {
        CurrentIconStatus = status;
        IconColor = status switch
        {
            "Connected" => "Green",
            "Disconnected" => "Red",
            "Retrying" => "Yellow",
            _ => "Gray"
        };

        IconStatusChanged?.Invoke(this, new IconStatusEventArgs { Status = status });
    }

    public void ShowBalloonNotification(string title, string message)
    {
        BalloonTipShown?.Invoke(this, new BalloonTipEventArgs
        {
            Title = title,
            Message = message
        });
    }

    public void MinimizeWindowToTray()
    {
        IsMinimizedToTray = true;
        WindowVisibilityChanged?.Invoke(this, new WindowVisibilityEventArgs { IsVisible = false });
    }

    public void RestoreWindowFromTray()
    {
        IsMinimizedToTray = false;
        WindowVisibilityChanged?.Invoke(this, new WindowVisibilityEventArgs { IsVisible = true });
    }

    public void OnTrayIconClick()
    {
        if (IsMinimizedToTray)
        {
            RestoreWindowFromTray();
        }
    }

    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}

public class BalloonTipEventArgs : EventArgs
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class WindowVisibilityEventArgs : EventArgs
{
    public bool IsVisible { get; set; }
}

public class IconStatusEventArgs : EventArgs
{
    public string Status { get; set; } = string.Empty;
}
