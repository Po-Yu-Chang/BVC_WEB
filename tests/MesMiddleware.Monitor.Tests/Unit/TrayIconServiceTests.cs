using FluentAssertions;
using Moq;
using MesMiddleware.Monitor.Services;

namespace MesMiddleware.Monitor.Tests.Unit;

/// <summary>
/// Unit tests for TrayIconService.
/// Tests FR-018 (minimize to system tray), FR-019 (notification balloon on errors).
/// RED PHASE: Test icon state changes (green/red/yellow), balloon notifications, minimize/restore behavior.
/// </summary>
public class TrayIconServiceTests
{
    [Fact]
    public void SetIcon_WhenConnected_ShouldShowGreenIcon()
    {
        // Arrange
        var trayService = new TrayIconService();

        // Act
        trayService.SetIconStatus("Connected");

        // Assert - FR-018: System tray icon reflects connection status
        trayService.CurrentIconStatus.Should().Be("Connected");
        trayService.IconColor.Should().Be("Green", "connected status should display green system tray icon");
    }

    [Fact]
    public void SetIcon_WhenDisconnected_ShouldShowRedIcon()
    {
        // Arrange
        var trayService = new TrayIconService();

        // Act
        trayService.SetIconStatus("Disconnected");

        // Assert
        trayService.CurrentIconStatus.Should().Be("Disconnected");
        trayService.IconColor.Should().Be("Red", "disconnected status should display red system tray icon");
    }

    [Fact]
    public void SetIcon_WhenRetrying_ShouldShowYellowIcon()
    {
        // Arrange
        var trayService = new TrayIconService();

        // Act
        trayService.SetIconStatus("Retrying");

        // Assert
        trayService.CurrentIconStatus.Should().Be("Retrying");
        trayService.IconColor.Should().Be("Yellow", "retrying status should display yellow system tray icon");
    }

    [Fact]
    public void ShowBalloonNotification_WhenUploadFails_ShouldDisplayErrorMessage()
    {
        // Arrange
        var trayService = new TrayIconService();
        var balloonShown = false;
        var balloonMessage = string.Empty;

        trayService.BalloonTipShown += (sender, args) =>
        {
            balloonShown = true;
            balloonMessage = args.Message;
        };

        // Act - FR-019: Show Windows notification balloon on critical errors
        trayService.ShowBalloonNotification("Upload Failed", "HTTP 503 - WebAPI unavailable for 5 minutes");

        // Assert
        balloonShown.Should().BeTrue("balloon notification should be displayed");
        balloonMessage.Should().Contain("HTTP 503", "error details should be shown in balloon");
    }

    [Fact]
    public void ShowBalloonNotification_WhenAuthenticationFails_ShouldDisplayErrorMessage()
    {
        // Arrange
        var trayService = new TrayIconService();
        var balloonShown = false;
        var balloonTitle = string.Empty;

        trayService.BalloonTipShown += (sender, args) =>
        {
            balloonShown = true;
            balloonTitle = args.Title;
        };

        // Act - FR-030: Detect auth failures and alert operator
        trayService.ShowBalloonNotification("Authentication Failed", "Invalid machine credentials");

        // Assert
        balloonShown.Should().BeTrue();
        balloonTitle.Should().Be("Authentication Failed");
    }

    [Fact]
    public void ShowBalloonNotification_WhenSharedMemoryError_ShouldDisplayErrorMessage()
    {
        // Arrange
        var trayService = new TrayIconService();
        var balloonShown = false;

        trayService.BalloonTipShown += (sender, args) =>
        {
            balloonShown = true;
        };

        // Act - FR-027: Log detailed error context, notify operator
        trayService.ShowBalloonNotification("Shared Memory Error", "Segment 'MES_INSPECTION_DATA' access denied");

        // Assert
        balloonShown.Should().BeTrue("critical shared memory errors should notify operator");
    }

    [Fact]
    public void MinimizeToTray_ShouldHideMainWindow()
    {
        // Arrange
        var trayService = new TrayIconService();
        var windowHidden = false;

        trayService.WindowVisibilityChanged += (sender, args) =>
        {
            if (args.IsVisible == false)
                windowHidden = true;
        };

        // Act - FR-018: Minimize to system tray
        trayService.MinimizeWindowToTray();

        // Assert
        windowHidden.Should().BeTrue("main window should be hidden when minimized to tray");
        trayService.IsMinimizedToTray.Should().BeTrue();
    }

    [Fact]
    public void RestoreFromTray_ShouldShowMainWindow()
    {
        // Arrange
        var trayService = new TrayIconService();
        trayService.MinimizeWindowToTray();

        var windowRestored = false;

        trayService.WindowVisibilityChanged += (sender, args) =>
        {
            if (args.IsVisible == true)
                windowRestored = true;
        };

        // Act - Click on tray icon should restore window
        trayService.RestoreWindowFromTray();

        // Assert
        windowRestored.Should().BeTrue("clicking tray icon should restore main window");
        trayService.IsMinimizedToTray.Should().BeFalse();
    }

    [Fact]
    public void TrayIconClick_WhenMinimized_ShouldRestoreWindow()
    {
        // Arrange
        var trayService = new TrayIconService();
        trayService.MinimizeWindowToTray();

        // Act - Simulate tray icon click
        trayService.OnTrayIconClick();

        // Assert
        trayService.IsMinimizedToTray.Should().BeFalse("tray icon click should restore window");
    }

    [Fact]
    public void StatusIconChange_WhenConnectionChanges_ShouldUpdateImmediately()
    {
        // Arrange
        var trayService = new TrayIconService();
        trayService.SetIconStatus("Connected"); // Green

        var iconChangedCount = 0;
        trayService.IconStatusChanged += (sender, args) =>
        {
            iconChangedCount++;
        };

        // Act - Connection status changes
        trayService.SetIconStatus("Disconnected"); // Red
        trayService.SetIconStatus("Retrying");     // Yellow
        trayService.SetIconStatus("Connected");    // Green again

        // Assert
        iconChangedCount.Should().Be(3, "icon should update for each status change");
        trayService.IconColor.Should().Be("Green", "final status should be reflected in icon");
    }

    [Fact]
    public void Dispose_ShouldCleanUpTrayIcon()
    {
        // Arrange
        var trayService = new TrayIconService();
        trayService.SetIconStatus("Connected");

        // Act
        trayService.Dispose();

        // Assert - Tray icon should be removed from system tray
        trayService.IsDisposed.Should().BeTrue("tray icon resources should be cleaned up");
    }
}
