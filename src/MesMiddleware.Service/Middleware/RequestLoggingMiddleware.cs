using System.Text;

namespace MesMiddleware.Service.Middleware;

/// <summary>
/// 記錄所有 HTTP 請求的 Middleware (用於除錯)
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

    public async Task InvokeAsync(HttpContext context)
    {
        // 記錄請求資訊
        _logger.LogInformation("========== 收到請求 ==========");
        _logger.LogInformation("方法: {Method}", context.Request.Method);
        _logger.LogInformation("路徑: {Path}", context.Request.Path);
        _logger.LogInformation("QueryString: {Query}", context.Request.QueryString);
        _logger.LogInformation("Content-Type: {ContentType}", context.Request.ContentType);

        // 如果是 POST/PUT，記錄 Body
        if (context.Request.Method == "POST" || context.Request.Method == "PUT")
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            // 只記錄前 500 字元
            var bodyPreview = body.Length > 500 ? body.Substring(0, 500) + "..." : body;
            _logger.LogInformation("Body: {Body}", bodyPreview);
        }

        // 執行下一個 Middleware
        await _next(context);

        // 記錄回應狀態
        _logger.LogInformation("回應狀態: {StatusCode}", context.Response.StatusCode);
        _logger.LogInformation("==============================");
    }
}

/// <summary>
/// 擴展方法
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
