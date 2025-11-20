using FluentAssertions;
using MesMiddleware.Monitor.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace MesMiddleware.Monitor.Tests.Integration;

/// <summary>
/// MiddlewareApiClient 與 MES Cloud API 整合測試
/// 測試追溯碼驗證、Token 注入、401 自動重試等功能
/// </summary>
public class MiddlewareApiClientMesCloudTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<MiddlewareApiClient>> _loggerMock;
    private readonly HttpClient _httpClient;

    public MiddlewareApiClientMesCloudTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<MiddlewareApiClient>>();

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
    }

    [Fact]
    public async Task VerifyTraceCodesAsync_WhenSuccessful_ShouldReturnSuccessResult()
    {
        // Arrange
        var expectedToken = "test-token-12345";
        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        var mesCloudResponse = new
        {
            success = true,
            data = new { validCount = 5, invalidCount = 0 },
            msg = "驗證成功",
            code = "0"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("/CimforceTraceMgrDev/api/transcode/checkcode")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mesCloudResponse))
            });

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        var traceCodes = new List<string> { "CODE001", "CODE002", "CODE003", "CODE004", "CODE005" };

        // Act
        var result = await apiClient.VerifyTraceCodesAsync("WO-12345", traceCodes);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("驗證成功");
        result.Code.Should().Be("0");

        // 驗證 Token 被注入到 header
        _tokenServiceMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyTraceCodesAsync_ShouldIncludeAccessTokenHeader()
    {
        // Arrange
        var expectedToken = "test-token-67890";
        HttpRequestMessage? capturedRequest = null;

        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        var mesCloudResponse = new
        {
            success = true,
            msg = "OK",
            code = "0"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mesCloudResponse))
            });

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        // Act
        await apiClient.VerifyTraceCodesAsync("WO-12345", new List<string> { "CODE001" });

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == "accessToken");
        capturedRequest.Headers.GetValues("accessToken").First().Should().Be(expectedToken);
    }

    [Fact]
    public async Task VerifyTraceCodesAsync_When401Unauthorized_ShouldRefreshTokenAndRetry()
    {
        // Arrange
        var firstToken = "expired-token";
        var secondToken = "new-token";
        var httpCallCount = 0;

        _tokenServiceMock
            .SetupSequence(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstToken)  // 第一次呼叫
            .ReturnsAsync(secondToken); // 第二次呼叫（refresh 後）

        _tokenServiceMock
            .Setup(x => x.RefreshTokenAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mesCloudResponse = new
        {
            success = true,
            msg = "OK",
            code = "0"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                httpCallCount++;
                // 第一次返回 401，第二次返回 200
                if (httpCallCount == 1)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.Unauthorized
                    };
                }
                else
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(JsonSerializer.Serialize(mesCloudResponse))
                    };
                }
            });

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        // Act
        var result = await apiClient.VerifyTraceCodesAsync("WO-12345", new List<string> { "CODE001" });

        // Assert
        result.Success.Should().BeTrue($"but got message: {result.Message}");
        // 驗證 RefreshTokenAsync 被呼叫
        _tokenServiceMock.Verify(x => x.RefreshTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
        // 驗證 GetAccessTokenAsync 被呼叫兩次（初次 + 重試）
        _tokenServiceMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        // 驗證 HTTP 請求被發送兩次（初次 + 重試）
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task VerifyTraceCodesAsync_WhenMesCloudReturnsError_ShouldReturnFailureResult()
    {
        // Arrange
        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");

        var mesCloudResponse = new
        {
            success = false,
            data = (object?)null,
            msg = "工單號不存在",
            code = "-1"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mesCloudResponse))
            });

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        // Act
        var result = await apiClient.VerifyTraceCodesAsync("INVALID-WO", new List<string> { "CODE001" });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("工單號不存在");
        result.Code.Should().Be("-1");
    }

    [Fact]
    public async Task VerifyTraceCodesAsync_WhenNetworkError_ShouldReturnFailureResult()
    {
        // Arrange
        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        // Act
        var result = await apiClient.VerifyTraceCodesAsync("WO-12345", new List<string> { "CODE001" });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("無法連線到 MES Cloud");
    }

    [Fact]
    public async Task VerifyTraceCodesAsync_ShouldSendCorrectPayload()
    {
        // Arrange
        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");

        string? capturedPayload = null;
        var mesCloudResponse = new
        {
            success = true,
            msg = "OK",
            code = "0"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedPayload = await req.Content!.ReadAsStringAsync(ct);
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(mesCloudResponse))
            });

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        var workOrderNumber = "WO-12345";
        var traceCodes = new List<string> { "CODE001", "CODE002" };

        // Act
        await apiClient.VerifyTraceCodesAsync(workOrderNumber, traceCodes);

        // Assert
        capturedPayload.Should().NotBeNull();
        var payloadObj = JsonSerializer.Deserialize<JsonElement>(capturedPayload!);
        payloadObj.GetProperty("woType").GetInt32().Should().Be(1);
        payloadObj.GetProperty("woNum").GetString().Should().Be(workOrderNumber);
        payloadObj.GetProperty("prtMacNo").GetString().Should().Be("MONITOR");
        payloadObj.GetProperty("codes").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task GetConnectionStatusAsync_WhenTokenServiceWorks_ShouldReturnConnected()
    {
        // Arrange
        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid-token");

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        // Act
        var status = await apiClient.GetConnectionStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.Status.Should().Be("Connected");
        _tokenServiceMock.Verify(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetConnectionStatusAsync_WhenTokenServiceFails_ShouldReturnDisconnected()
    {
        // Arrange
        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("MES Cloud unreachable"));

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        // Act
        var status = await apiClient.GetConnectionStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.Status.Should().Be("Disconnected");
    }

    [Fact]
    public async Task GetConnectionStatusAsync_WhenTokenIsEmpty_ShouldReturnDisconnected()
    {
        // Arrange
        _tokenServiceMock
            .Setup(x => x.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var apiClient = new MiddlewareApiClient(
            "http://localhost:8080",
            _httpClient,
            _tokenServiceMock.Object,
            _loggerMock.Object);

        // Act
        var status = await apiClient.GetConnectionStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.Status.Should().Be("Disconnected");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
