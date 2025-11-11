using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MesMiddleware.Monitor.ViewModels;
using MesMiddleware.Monitor.Services;
using MesMiddleware.Monitor.Models;
using System.Collections.ObjectModel;

namespace MesMiddleware.Monitor.Tests.Unit;

/// <summary>
/// Unit tests for HistoryViewModel.
/// Tests FR-016 (display upload history), ObservableCollection, filtering, paging.
/// RED PHASE: Test history loading, filtering, paging (max 1000 records).
/// </summary>
public class HistoryViewModelTests
{
    private static Mock<ILogger<HistoryViewModel>> CreateMockLogger()
    {
        return new Mock<ILogger<HistoryViewModel>>();
    }

    [Fact]
    public void UploadRecords_ShouldBeObservableCollection()
    {
        // Arrange & Act
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        var viewModel = new HistoryViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Assert
        viewModel.UploadRecords.Should().BeOfType<ObservableCollection<UploadRecord>>(
            "WPF DataGrid requires ObservableCollection for data binding");
    }

    [Fact]
    public async Task LoadHistoryAsync_ShouldPopulateUploadRecords()
    {
        // Arrange
        var testRecords = new List<UploadRecord>
        {
            new() { Timestamp = DateTime.UtcNow, TraceCode = "TRACE001", EquipmentName = "MACHINE-01", Status = "Success", ErrorMessage = null },
            new() { Timestamp = DateTime.UtcNow.AddMinutes(-5), TraceCode = "TRACE002", EquipmentName = "MACHINE-02", Status = "Failed", ErrorMessage = "HTTP 503" },
            new() { Timestamp = DateTime.UtcNow.AddMinutes(-10), TraceCode = "TRACE003", EquipmentName = "MACHINE-01", Status = "Pending", ErrorMessage = null }
        };

        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetUploadHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testRecords);

        var viewModel = new HistoryViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Act
        await viewModel.LoadHistoryAsync();

        // Assert - FR-016: Display upload history
        viewModel.UploadRecords.Should().HaveCount(3);
        viewModel.UploadRecords[0].TraceCode.Should().Be("TRACE001");
        viewModel.UploadRecords[1].TraceCode.Should().Be("TRACE002");
        viewModel.UploadRecords[2].TraceCode.Should().Be("TRACE003");

        // Verify correct properties are present
        viewModel.UploadRecords[0].Timestamp.Should().NotBe(default);
        viewModel.UploadRecords[0].EquipmentName.Should().Be("MACHINE-01");
        viewModel.UploadRecords[0].Status.Should().Be("Success");

        viewModel.UploadRecords[1].Status.Should().Be("Failed");
        viewModel.UploadRecords[1].ErrorMessage.Should().Contain("HTTP 503");
    }

    [Fact]
    public async Task LoadHistoryAsync_ShouldLimitTo1000Records()
    {
        // Arrange - Simulate 1500 records from API
        var mockRecords = Enumerable.Range(1, 1500).Select(i => new UploadRecord
        {
            Timestamp = DateTime.UtcNow.AddMinutes(-i),
            TraceCode = $"TRACE{i:D4}",
            EquipmentName = "MACHINE-01",
            Status = "Success",
            ErrorMessage = null
        }).ToList();

        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetUploadHistoryAsync(1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockRecords.Take(1000).ToList());

        var viewModel = new HistoryViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Act
        await viewModel.LoadHistoryAsync();

        // Assert - Should limit to 1000 max records (per task description)
        viewModel.UploadRecords.Should().HaveCount(1000, "UI should not load more than 1000 records for performance");
        mockApiClient.Verify(x => x.GetUploadHistoryAsync(1000, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FilterByTraceCode_ShouldFilterRecords()
    {
        // Arrange
        var testRecords = new List<UploadRecord>
        {
            new() { Timestamp = DateTime.UtcNow, TraceCode = "TRACE_A_001", EquipmentName = "MACHINE-01", Status = "Success" },
            new() { Timestamp = DateTime.UtcNow, TraceCode = "TRACE_B_002", EquipmentName = "MACHINE-02", Status = "Success" },
            new() { Timestamp = DateTime.UtcNow, TraceCode = "TRACE_A_003", EquipmentName = "MACHINE-01", Status = "Failed" }
        };

        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetUploadHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testRecords);

        var viewModel = new HistoryViewModel(mockApiClient.Object, CreateMockLogger().Object);
        await viewModel.LoadHistoryAsync();

        // Act - Apply filter
        viewModel.FilterText = "TRACE_A";
        viewModel.ApplyFilter();

        // Assert - Should show only TRACE_A records
        viewModel.FilteredRecords.Should().HaveCount(2);
        viewModel.FilteredRecords.Should().AllSatisfy(r => r.TraceCode.Should().Contain("TRACE_A"));
    }

    [Fact]
    public async Task FilterByStatus_ShouldFilterRecords()
    {
        // Arrange
        var testRecords = new List<UploadRecord>
        {
            new() { TraceCode = "TRACE001", Status = "Success" },
            new() { TraceCode = "TRACE002", Status = "Failed" },
            new() { TraceCode = "TRACE003", Status = "Success" },
            new() { TraceCode = "TRACE004", Status = "Pending" }
        };

        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetUploadHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testRecords);

        var viewModel = new HistoryViewModel(mockApiClient.Object, CreateMockLogger().Object);
        await viewModel.LoadHistoryAsync();

        // Act - Filter by Failed status
        viewModel.StatusFilter = "Failed";
        viewModel.ApplyFilter();

        // Assert
        viewModel.FilteredRecords.Should().HaveCount(1);
        viewModel.FilteredRecords[0].Status.Should().Be("Failed");
    }

    [Fact]
    public async Task ClearFilter_ShouldShowAllRecords()
    {
        // Arrange
        var testRecords = new List<UploadRecord>
        {
            new() { TraceCode = "TRACE001", Status = "Success" },
            new() { TraceCode = "TRACE002", Status = "Failed" }
        };

        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetUploadHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testRecords);

        var viewModel = new HistoryViewModel(mockApiClient.Object, CreateMockLogger().Object);
        await viewModel.LoadHistoryAsync();

        viewModel.FilterText = "TRACE001";
        viewModel.ApplyFilter();

        viewModel.FilteredRecords.Should().HaveCount(1);

        // Act - Clear filter
        viewModel.ClearFilter();

        // Assert - Should show all records again
        viewModel.FilteredRecords.Should().HaveCount(2);
    }

    [Fact]
    public void PropertyChanged_WhenFilterTextChanges_ShouldRaiseEvent()
    {
        // Arrange
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        var viewModel = new HistoryViewModel(mockApiClient.Object, CreateMockLogger().Object);

        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == "FilterText")
                propertyChangedRaised = true;
        };

        // Act
        viewModel.FilterText = "TRACE001";

        // Assert
        propertyChangedRaised.Should().BeTrue("FilterText property change should notify UI");
    }

    [Fact]
    public async Task LoadHistoryAsync_WhenApiThrowsException_ShouldShowError()
    {
        // Arrange
        var mockApiClient = new Mock<IMiddlewareApiClient>();
        mockApiClient
            .Setup(x => x.GetUploadHistoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var viewModel = new HistoryViewModel(mockApiClient.Object, CreateMockLogger().Object);

        // Act
        await viewModel.LoadHistoryAsync();

        // Assert - Should gracefully handle errors
        viewModel.UploadRecords.Should().BeEmpty();
        viewModel.ErrorMessage.Should().Contain("Database connection failed");
    }
}
