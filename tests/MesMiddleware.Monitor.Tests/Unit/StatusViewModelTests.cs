using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.ViewModels;
using MesMiddleware.Monitor.Services;
using System.ComponentModel;
using System.Net.Http;

namespace MesMiddleware.Monitor.Tests.Unit;

/// <summary>
/// Unit tests for StatusViewModel.
/// Tests FR-015 (display connection status), INotifyPropertyChanged, status states.
/// RED PHASE: Test ViewModel behavior, connection status updates, property change notifications.
/// </summary>
public class StatusViewModelTests
{
    private static Mock<ILogger<StatusViewModel>> CreateMockLogger()
    {
        return new Mock<ILogger<StatusViewModel>>();
    }

    [Fact]
    public void StatusViewModel_ShouldImplementINotifyPropertyChanged()
    {
        // Arrange
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto
            {
                Status = "Connected",
                LastPingTimestamp = DateTime.UtcNow
            });
        var mockLogger = CreateMockLogger();

        // Act
        var viewModel = new StatusViewModel(mockApiClient.Object, mockLogger.Object);

        // Assert
        viewModel.Should().BeAssignableTo<INotifyPropertyChanged>(
            "ViewModel must implement INotifyPropertyChanged for WPF binding");
    }

    [Fact]
    public async Task ConnectionStatus_WhenConnected_ShouldShowGreenStatus()
    {
        // Arrange
        var lastPing = DateTime.UtcNow;
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto
            {
                Status = "Connected",
                LastPingTimestamp = lastPing,
                QueueDepth = 0
            });

        var viewModel = new StatusViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Act
        await viewModel.RefreshStatusAsync();

        // Assert - FR-015: Display connection status
        viewModel.ConnectionStatus.Should().Be("Connected");
        viewModel.StatusColor.Should().Be("Green", "connected status should display green indicator");
        viewModel.LastPingTime.Should().BeCloseTo(lastPing, TimeSpan.FromSeconds(1));
        viewModel.QueueDepth.Should().Be(0);
    }

    [Fact]
    public async Task ConnectionStatus_WhenDisconnected_ShouldShowRedStatus()
    {
        // Arrange
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto
            {
                Status = "Disconnected",
                LastPingTimestamp = DateTime.UtcNow.AddMinutes(-5)
            });

        var viewModel = new StatusViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Act
        await viewModel.RefreshStatusAsync();

        // Assert
        viewModel.ConnectionStatus.Should().Be("Disconnected");
        viewModel.StatusColor.Should().Be("Red", "disconnected status should display red indicator");
    }

    [Fact]
    public async Task ConnectionStatus_WhenRetrying_ShouldShowYellowStatus()
    {
        // Arrange
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto
            {
                Status = "Retrying",
                LastPingTimestamp = DateTime.UtcNow.AddSeconds(-10)
            });

        var viewModel = new StatusViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Act
        await viewModel.RefreshStatusAsync();

        // Assert
        viewModel.ConnectionStatus.Should().Be("Retrying");
        viewModel.StatusColor.Should().Be("Yellow", "retrying status should display yellow indicator");
    }

    [Fact]
    public void PropertyChanged_WhenConnectionStatusChanges_ShouldRaiseEvent()
    {
        // Arrange
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionStatusDto
            {
                Status = "Connected",
                LastPingTimestamp = DateTime.UtcNow
            });

        var viewModel = new StatusViewModel(mockApiClient.Object, CreateMockLogger().Object);

        var propertyChangedEvents = new List<string>();
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
                propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        viewModel.RefreshStatusAsync().Wait();

        // Assert - INotifyPropertyChanged should fire for data-bound properties
        propertyChangedEvents.Should().Contain("ConnectionStatus", "binding should update when status changes");
        propertyChangedEvents.Should().Contain("StatusColor", "UI color indicator should update");
    }

    [Fact]
    public async Task RefreshStatusAsync_WhenApiThrowsException_ShouldShowErrorStatus()
    {
        // Arrange
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var viewModel = new StatusViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Act
        await viewModel.RefreshStatusAsync();

        // Assert - Should gracefully handle API failures
        viewModel.ConnectionStatus.Should().Be("Error");
        viewModel.StatusColor.Should().Be("Red");
        viewModel.ErrorMessage.Should().Contain("Service unavailable");
    }

    [Fact]
    public async Task AutoRefresh_ShouldUpdateEvery2Seconds()
    {
        // Arrange
        var callCount = 0;
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new ConnectionStatusDto
                {
                    Status = "Connected",
                    LastPingTimestamp = DateTime.UtcNow
                };
            });

        var viewModel = new StatusViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Act - Start auto-refresh with 100ms interval (for testing)
        viewModel.StartAutoRefresh(TimeSpan.FromMilliseconds(100));

        await Task.Delay(350); // Wait for ~3 refresh cycles

        viewModel.StopAutoRefresh();

        // Assert - FR-020: Auto-refresh UI every 2 seconds
        callCount.Should().BeGreaterThanOrEqualTo(2, "auto-refresh should poll API multiple times");
    }
}
