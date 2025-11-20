using FluentAssertions;
using MesMiddleware.Monitor.Models;
using MesMiddleware.Monitor.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace MesMiddleware.Monitor.Tests.Unit;

/// <summary>
/// TokenService 單元測試
/// 測試 Token 取得、快取、更新和過期檢查
/// </summary>
public class TokenServiceTests : IDisposable
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ILogger<TokenService>> _loggerMock;
    private readonly IOptions<WebApiOptions> _options;
    private readonly HttpClient _httpClient;

    public TokenServiceTests()
    {
        // 重設靜態快取（防止測試間互相干擾）
        TokenService.ResetCacheForTesting();

        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _loggerMock = new Mock<ILogger<TokenService>>();

        _options = Options.Create(new WebApiOptions
        {
            BaseUrl = "http://localhost:8080",
            MachineNumber = "TEST-MACHINE",
            MachineIp = "192.168.1.100",
            Timeout = 30
        });

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri(_options.Value.BaseUrl)
        };

        _httpClientFactoryMock
            .Setup(x => x.CreateClient("MesCloudClient"))
            .Returns(_httpClient);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenCalledFirstTime_ShouldRequestNewTokenFromMesCloud()
    {
        // Arrange
        var expectedToken = "test-token-12345";
        var loginResponse = new
        {
            success = true,
            data = new
            {
                prtMacNo = "TEST-MACHINE",
                ipAddr = "192.168.1.100",
                token = expectedToken,
                sysUserId = "user123"
            },
            msg = "登入成功",
            code = "0"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("/CimforceTraceMgrDev/api/prtmac/prtmacuserlogin")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(loginResponse))
            });

        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act
        var token = await tokenService.GetAccessTokenAsync();

        // Assert
        token.Should().Be(expectedToken);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("/CimforceTraceMgrDev/api/prtmac/prtmacuserlogin")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenTokenIsCached_ShouldReturnCachedToken()
    {
        // Arrange
        var expectedToken = "cached-token-67890";
        var loginResponse = new
        {
            success = true,
            data = new
            {
                token = expectedToken
            }
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
                Content = new StringContent(JsonSerializer.Serialize(loginResponse))
            });

        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act - 第一次呼叫，會請求新 token
        var token1 = await tokenService.GetAccessTokenAsync();
        // Act - 第二次呼叫，應該返回快取的 token
        var token2 = await tokenService.GetAccessTokenAsync();

        // Assert
        token1.Should().Be(expectedToken);
        token2.Should().Be(expectedToken);
        // 只應該呼叫一次 HTTP 請求（第二次使用快取）
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenLoginFails_ShouldThrowException()
    {
        // Arrange
        var loginResponse = new
        {
            success = false,
            msg = "設備編號不存在",
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
                Content = new StringContent(JsonSerializer.Serialize(loginResponse))
            });

        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act & Assert
        var act = async () => await tokenService.GetAccessTokenAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*MES Cloud login failed*");
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenHttpRequestFails_ShouldThrowException()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act & Assert
        var act = async () => await tokenService.GetAccessTokenAsync();
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldAddReferrerHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var loginResponse = new
        {
            success = true,
            data = new { token = "test-token" }
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
                Content = new StringContent(JsonSerializer.Serialize(loginResponse))
            });

        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act
        await tokenService.GetAccessTokenAsync();

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().Contain(h => h.Key == "Referrer");
        capturedRequest.Headers.GetValues("Referrer").First().Should().Be(_options.Value.MachineIp);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldInvalidateCacheAndRequestNewToken()
    {
        // Arrange
        var firstToken = "first-token";
        var secondToken = "second-token";
        var callCount = 0;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var token = callCount == 1 ? firstToken : secondToken;
                var response = new
                {
                    success = true,
                    data = new { token }
                };
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(response))
                };
            });

        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act
        var token1 = await tokenService.GetAccessTokenAsync(); // 第一次取得
        await tokenService.RefreshTokenAsync(); // 強制更新
        var token2 = await tokenService.GetAccessTokenAsync(); // 更新後取得

        // Assert
        token1.Should().Be(firstToken);
        token2.Should().Be(secondToken);
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2), // 應該呼叫兩次（初次 + 更新）
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public void IsTokenValid_WhenTokenIsNull_ShouldReturnFalse()
    {
        // Arrange
        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act
        var isValid = tokenService.IsTokenValid();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task IsTokenValid_WhenTokenIsCached_ShouldReturnTrue()
    {
        // Arrange
        var loginResponse = new
        {
            success = true,
            data = new { token = "test-token" }
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
                Content = new StringContent(JsonSerializer.Serialize(loginResponse))
            });

        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act
        await tokenService.GetAccessTokenAsync(); // 快取 token
        var isValid = tokenService.IsTokenValid();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task GetAccessTokenAsync_ConcurrentCalls_ShouldRequestTokenOnlyOnce()
    {
        // Arrange
        var loginResponse = new
        {
            success = true,
            data = new { token = "test-token" }
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
                Content = new StringContent(JsonSerializer.Serialize(loginResponse))
            });

        var tokenService = new TokenService(_httpClientFactoryMock.Object, _options, _loggerMock.Object);

        // Act - 同時發起 10 個請求
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => tokenService.GetAccessTokenAsync())
            .ToArray();

        var tokens = await Task.WhenAll(tasks);

        // Assert
        tokens.Should().AllBe("test-token");
        // 由於有 SemaphoreSlim 鎖定，只應該呼叫一次
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
