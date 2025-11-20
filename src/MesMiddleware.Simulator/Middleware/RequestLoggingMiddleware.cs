using MesMiddleware.Simulator.Data;
using MesMiddleware.Simulator.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MesMiddleware.Simulator.Middleware;

/// <summary>
/// HTTP 請求日誌中間件 - 記錄所有請求和響應的完整通訊資料
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, SimulatorDbContext dbContext)
    {
        // 跳過 Swagger 和靜態文件
        if (context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.StartsWithSegments("/favicon.ico"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        // 讀取請求 Body
        context.Request.EnableBuffering();
        var requestBody = await ReadBodyAsync(context.Request.Body);
        context.Request.Body.Position = 0;

        // 讀取請求 Headers
        var requestHeaders = JsonSerializer.Serialize(
            context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));

        // 替換響應 Stream 以便讀取
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        Exception? exception = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // 讀取響應 Body
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseBodyText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);

            // 將響應寫回原始 Stream
            await responseBody.CopyToAsync(originalBodyStream);

            // 保存請求日誌到數據庫
            var requestLog = new RequestLog
            {
                Timestamp = DateTime.Now,
                Method = context.Request.Method,
                Endpoint = context.Request.Path + context.Request.QueryString,
                ClientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                RequestHeaders = requestHeaders,
                RequestBody = requestBody,
                ResponseStatusCode = context.Response.StatusCode,
                ResponseBody = responseBodyText,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                IsSuccess = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300,
                ErrorMessage = exception?.Message
            };

            try
            {
                dbContext.RequestLogs.Add(requestLog);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存請求日誌失敗");
            }
        }
    }

    private static async Task<string> ReadBodyAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        stream.Position = 0;
        return body;
    }
}
