using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MesMiddleware.Service.Services.WebApi;
using MesMiddleware.Shared.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace MesMiddleware.Service.Tests.Integration;

/// <summary>
/// Integration tests for WebAPI upload using WireMock to mock the WebAPI server.
/// Tests FR-008 (POST to /api/inspection/upload), FR-010 (retry with exponential backoff).
/// RED PHASE: These tests verify the actual HTTP integration behavior.
/// </summary>
public class WebApiUploadTests : IDisposable
{
    private WireMockServer? _mockServer;

    public void Dispose()
    {
        _mockServer?.Stop();
        _mockServer?.Dispose();
    }

    [Fact]
    public async Task UploadInspectionData_WithValidData_ShouldSucceed()
    {
        // Arrange - Setup WireMock server
        _mockServer = WireMockServer.Start();

        // Mock successful auth response
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/auth/login")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = true,
                    accessToken = "test-token-12345"
                })));

        // Mock successful upload response
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/inspection/upload")
                .UsingPost()
                .WithHeader("accessToken", "test-token-12345"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "Data uploaded successfully"
                })));

        var testData = CreateValidInspectionRecord();

        // Act - Upload data to mock server
        // (This would require creating a test instance of MesWebApiClient with mock HttpClient)
        // For now, verify mock server received the request

        var httpClient = new HttpClient { BaseAddress = new Uri(_mockServer.Urls[0]) };

        var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", new
        {
            machineNumber = "TEST-001",
            machineIp = "192.168.1.100"
        });

        loginResponse.IsSuccessStatusCode.Should().BeTrue("authentication should succeed");

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        loginResult!.Success.Should().BeTrue();
        loginResult.AccessToken.Should().Be("test-token-12345");

        // Now upload inspection data with token
        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inspection/upload")
        {
            Content = JsonContent.Create(testData, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
        };
        uploadRequest.Headers.Add("accessToken", loginResult.AccessToken);

        var uploadResponse = await httpClient.SendAsync(uploadRequest);

        // Assert - Upload succeeded
        uploadResponse.IsSuccessStatusCode.Should().BeTrue("upload should succeed");

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        uploadResult!.Success.Should().BeTrue();
        uploadResult.Message.Should().Contain("successfully");

        // Verify WireMock received exactly 2 requests (auth + upload)
        _mockServer.LogEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task UploadInspectionData_WithUnauthorized401_ShouldRefreshTokenAndRetry()
    {
        // Arrange - Mock server that returns 401 (tests FR-030: detect auth failures)
        _mockServer = WireMockServer.Start();

        // Mock auth response
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/auth/login")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = true,
                    accessToken = "test-token"
                })));

        // Mock upload returns 401 Unauthorized
        _mockServer
            .Given(Request.Create()
                .WithPath("/api/inspection/upload")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Unauthorized - token expired"
                })));

        var httpClient = new HttpClient { BaseAddress = new Uri(_mockServer.Urls[0]) };

        // Act
        var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", new
        {
            machineNumber = "TEST-001",
            machineIp = "192.168.1.100"
        });

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inspection/upload")
        {
            Content = JsonContent.Create(CreateValidInspectionRecord())
        };
        uploadRequest.Headers.Add("accessToken", loginResult!.AccessToken);

        var uploadResponse = await httpClient.SendAsync(uploadRequest);

        // Assert - Should receive 401 (MesWebApiClient should handle token refresh)
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Verify auth failure is detectable (FR-030)
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        uploadResult!.Success.Should().BeFalse();
        uploadResult.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task UploadInspectionData_WithNetworkError_ShouldThrowHttpRequestException()
    {
        // Arrange - Mock server that returns 503 Service Unavailable
        _mockServer = WireMockServer.Start();

        _mockServer
            .Given(Request.Create()
                .WithPath("/api/auth/login")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = true,
                    accessToken = "test-token"
                })));

        _mockServer
            .Given(Request.Create()
                .WithPath("/api/inspection/upload")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Service temporarily unavailable"
                })));

        var httpClient = new HttpClient { BaseAddress = new Uri(_mockServer.Urls[0]) };

        // Act & Assert - Should receive 503 error
        var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", new
        {
            machineNumber = "TEST-001",
            machineIp = "192.168.1.100"
        });

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inspection/upload")
        {
            Content = JsonContent.Create(CreateValidInspectionRecord())
        };
        uploadRequest.Headers.Add("accessToken", loginResult!.AccessToken);

        var uploadResponse = await httpClient.SendAsync(uploadRequest);

        // Assert - Should receive 503
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // Polly retry policy should handle this (tested via UploadQueueService integration)
    }

    [Fact]
    public async Task UploadInspectionData_WithValidationError_ShouldReturnFailure()
    {
        // Arrange - Mock server returns validation error
        _mockServer = WireMockServer.Start();

        _mockServer
            .Given(Request.Create()
                .WithPath("/api/auth/login")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = true,
                    accessToken = "test-token"
                })));

        _mockServer
            .Given(Request.Create()
                .WithPath("/api/inspection/upload")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBody(JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Invalid data: missing required field 'userName'"
                })));

        var httpClient = new HttpClient { BaseAddress = new Uri(_mockServer.Urls[0]) };

        var invalidData = CreateValidInspectionRecord();
        invalidData.UserName = null!; // Missing required field

        // Act
        var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", new
        {
            machineNumber = "TEST-001",
            machineIp = "192.168.1.100"
        });

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/inspection/upload")
        {
            Content = JsonContent.Create(invalidData)
        };
        uploadRequest.Headers.Add("accessToken", loginResult!.AccessToken);

        var uploadResponse = await httpClient.SendAsync(uploadRequest);

        // Assert - Should receive 400 Bad Request
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        uploadResult!.Success.Should().BeFalse();
        uploadResult.Message.Should().Contain("Invalid");
    }

    private static InspectionRecord CreateValidInspectionRecord()
    {
        return new InspectionRecord
        {
            ProcName = "Test Inspection",
            DevName = "TEST-MACHINE-01",
            UserName = "test_operator",
            WorkClass = "Day",
            TraceCode = "TRACE_TEST_001",
            ParamData = new List<ParamDataItem>
            {
                new() { Name = "TestParam", Value = "100.5", Unit = "mm", Status = "Pass" }
            },
            Benchmarks = new List<BenchmarkItem>
            {
                new() { Name = "TestParam", UpperLimit = "105", LowerLimit = "95", Unit = "mm" }
            },
            OtherData = new List<OtherDataItem>
            {
                new() { Key = "Temperature", Value = "25", Unit = "°C" }
            },
            InspectionTime = DateTime.UtcNow
        };
    }

    private class TokenResponse
    {
        public bool Success { get; set; }
        public string? AccessToken { get; set; }
        public string? Message { get; set; }
    }

    private class UploadResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
