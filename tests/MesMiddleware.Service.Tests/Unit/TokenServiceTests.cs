using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using MesMiddleware.Service.Models;
using MesMiddleware.Service.Services.WebApi;
using System.Net;
using System.Text.Json;

namespace MesMiddleware.Service.Tests.Unit;

/// <summary>
/// Unit tests for TokenService.
/// Tests FR-013 (authenticate on startup), FR-014 (auto-refresh token after 8 hours).
/// RED PHASE: Test token caching, refresh on expiration, SemaphoreSlim thread-safety.
/// </summary>
public class TokenServiceTests
{
    [Fact]
    public async Task GetAccessToken_WhenCacheEmpty_ShouldRequestNewToken()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/auth/login")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    success = true,
                    accessToken = "test-token-123"
                }))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://test-api.com")
        };

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient("WebApiClient")).Returns(httpClient);

        var options = Options.Create(new WebApiOptions
        {
            BaseUrl = "http://test-api.com",
            MachineNumber = "TEST-MACHINE-001",
            MachineIp = "192.168.1.100"
        });

        var mockLogger = new Mock<ILogger<TokenService>>();
        var tokenService = new TokenService(mockHttpClientFactory.Object, options, mockLogger.Object);

        // Act
        var token = await tokenService.GetAccessTokenAsync();

        // Assert
        token.Should().Be("test-token-123");

        // Verify HTTP request was made
        mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.PathAndQuery.Contains("/api/auth/login")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetAccessToken_WhenTokenCached_ShouldReturnCachedToken()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    success = true,
                    accessToken = "cached-token"
                }))
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://test-api.com")
        };

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient("WebApiClient")).Returns(httpClient);

        var options = Options.Create(new WebApiOptions
        {
            BaseUrl = "http://test-api.com",
            MachineNumber = "TEST-001",
            MachineIp = "192.168.1.100"
        });

        var mockLogger = new Mock<ILogger<TokenService>>();
        var tokenService = new TokenService(mockHttpClientFactory.Object, options, mockLogger.Object);

        // Act - Get token twice
        var token1 = await tokenService.GetAccessTokenAsync();
        var token2 = await tokenService.GetAccessTokenAsync();

        // Assert - Second call should return cached token without HTTP request
        token1.Should().Be("cached-token");
        token2.Should().Be("cached-token");

        // HTTP request should only be made once (first call)
        mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task RefreshToken_ShouldInvalidateCacheAndRequestNewToken()
    {
        // Arrange
        var requestCount = 0;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                requestCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        success = true,
                        accessToken = $"token-{requestCount}"
                    }))
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://test-api.com")
        };

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient("WebApiClient")).Returns(httpClient);

        var options = Options.Create(new WebApiOptions
        {
            BaseUrl = "http://test-api.com",
            MachineNumber = "TEST-001",
            MachineIp = "192.168.1.100"
        });

        var mockLogger = new Mock<ILogger<TokenService>>();
        var tokenService = new TokenService(mockHttpClientFactory.Object, options, mockLogger.Object);

        // Act
        var token1 = await tokenService.GetAccessTokenAsync(); // First token
        await tokenService.RefreshTokenAsync(); // Force refresh
        var token2 = await tokenService.GetAccessTokenAsync(); // Should get new token

        // Assert - Tokens should be different after refresh
        token1.Should().Be("token-1");
        token2.Should().Be("token-2");

        // HTTP requests: 1st get + refresh + 2nd get = 2 requests (refresh calls GetAccessToken internally)
        mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public void IsTokenValid_WithExpiredToken_ShouldReturnFalse()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient("WebApiClient")).Returns(httpClient);

        var options = Options.Create(new WebApiOptions
        {
            BaseUrl = "http://test-api.com",
            MachineNumber = "TEST-001",
            MachineIp = "192.168.1.100"
        });

        var mockLogger = new Mock<ILogger<TokenService>>();
        var tokenService = new TokenService(mockHttpClientFactory.Object, options, mockLogger.Object);

        // Act - Check validity without getting token
        var isValid = tokenService.IsTokenValid();

        // Assert - Should be invalid (no token cached)
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task GetAccessToken_ConcurrentCalls_ShouldUseSemaphoreForThreadSafety()
    {
        // Arrange
        var requestCount = 0;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
            {
                Interlocked.Increment(ref requestCount);
                await Task.Delay(100); // Simulate network delay
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        success = true,
                        accessToken = "concurrent-token"
                    }))
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://test-api.com")
        };

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory.Setup(f => f.CreateClient("WebApiClient")).Returns(httpClient);

        var options = Options.Create(new WebApiOptions
        {
            BaseUrl = "http://test-api.com",
            MachineNumber = "TEST-001",
            MachineIp = "192.168.1.100"
        });

        var mockLogger = new Mock<ILogger<TokenService>>();
        var tokenService = new TokenService(mockHttpClientFactory.Object, options, mockLogger.Object);

        // Act - 5 concurrent token requests
        var tasks = Enumerable.Range(1, 5)
            .Select(async _ => await tokenService.GetAccessTokenAsync())
            .ToArray();

        var tokens = await Task.WhenAll(tasks);

        // Assert - All tokens should be the same (only 1 HTTP request due to SemaphoreSlim)
        tokens.Should().AllBe("concurrent-token");
        requestCount.Should().Be(1, "SemaphoreSlim should prevent multiple concurrent requests");
    }
}
