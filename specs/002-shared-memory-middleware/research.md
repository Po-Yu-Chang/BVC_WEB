# Technology Research: Shared Memory Middleware Service

**Feature**: 002-shared-memory-middleware
**Date**: 2025-11-11
**Purpose**: Document technology decisions for Windows shared memory IPC, WPF monitoring UI, and WebAPI integration

## R1: Windows Shared Memory IPC Pattern

**Decision**: MemoryMappedFile with EventWaitHandle for event-driven notifications

**Rationale**:
- **MemoryMappedFile** provides named shared memory segments accessible across processes, supports large payloads (10MB+), and offers fast read/write without serialization overhead for raw bytes
- **EventWaitHandle** enables event-driven signaling - equipment signals middleware immediately when data ready, avoiding polling overhead
- **Mutex** ensures exclusive access during read/write operations, preventing corrupted data from concurrent access
- Combination achieves <1 second detection latency (requirement) with minimal CPU usage

**Alternatives Considered**:
- **Named Pipes**: More complex API, streaming-oriented (requires framing), higher latency for large payloads
- **WCF NetNamedPipeBinding**: Deprecated in .NET Core, heavy-weight for simple IPC
- **Memory-mapped files with polling**: Wastes CPU, cannot guarantee <1s latency, unacceptable for production
- **File system watching (FileSystemWatcher)**: High latency (typically 1-5s), unreliable for fast writes, file I/O overhead

**Implementation Notes**:
```csharp
// Equipment writes data
using var mmf = MemoryMappedFile.CreateOrOpen("MES_INSPECTION_DATA", 10_000_000);
using var accessor = mmf.CreateViewAccessor();
accessor.Write(0, jsonBytes.Length);
accessor.WriteArray(4, jsonBytes, 0, jsonBytes.Length);
using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "MES_DATA_READY");
evt.Set(); // Signal middleware

// Middleware reads data
using var mmf = MemoryMappedFile.OpenExisting("MES_INSPECTION_DATA");
using var evt = EventWaitHandle.OpenExisting("MES_DATA_READY");
evt.WaitOne(); // Block until signal
using var accessor = mmf.CreateViewAccessor();
int length = accessor.ReadInt32(0);
byte[] data = new byte[length];
accessor.ReadArray(4, data, 0, length);
```

**References**:
- https://learn.microsoft.com/en-us/dotnet/standard/io/memory-mapped-files
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.eventwaithandle

---

## R2: Windows Service Hosting Pattern

**Decision**: .NET 9 BackgroundService with Microsoft.Extensions.Hosting

**Rationale**:
- **BackgroundService** provides robust lifecycle management (StartAsync/StopAsync/ExecuteAsync) with built-in cancellation token support for graceful shutdown
- **Microsoft.Extensions.Hosting** integrates dependency injection, configuration, logging without manual wiring
- **Windows Service hosting** via `UseWindowsService()` extension method - minimal code to run as NT Service
- Supports appsettings.json configuration, Serilog integration, and health checks out-of-the-box

**Alternatives Considered**:
- **TopShelf**: Third-party library, less maintained, adds unnecessary abstraction over built-in hosting
- **Manual Service class (ServiceBase)**: More boilerplate, manual DI wiring, no modern patterns
- **Worker Service template**: Same as BackgroundService approach - this is the modern standard

**Implementation Notes**:
```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => { options.ServiceName = "MesMiddlewareService"; });
builder.Services.AddHostedService<MiddlewareHostedService>();
builder.Services.AddSingleton<ISharedMemoryMonitor, SharedMemoryMonitor>();
builder.Services.AddHttpClient<IMesWebApiClient, MesWebApiClient>()
    .AddStandardResilienceHandler(); // Polly retry/circuit breaker
await builder.Build().RunAsync();

// Install: sc create MesMiddlewareService binPath="C:\Path\To\MesMiddleware.Service.exe"
```

**References**:
- https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services

---

## R3: WPF MVVM Pattern with System Tray

**Decision**: WPF .NET 9 with MVVM pattern, Hardcodet.NotifyIcon.Wpf for system tray

**Rationale**:
- **WPF .NET 9** provides modern desktop UI with XAML data binding, theming support, and .NET ecosystem integration
- **MVVM pattern** separates UI logic (ViewModels) from views, enabling unit testing of UI logic without UI thread
- **Hardcodet.NotifyIcon.Wpf** (or H.NotifyIcon.Wpf in .NET 9) provides system tray icon with balloon notifications, context menu, and minimize-to-tray behavior
- **ObservableCollection** + **INotifyPropertyChanged** enable automatic UI updates when service state changes (no manual Dispatcher.Invoke needed for simple bindings)

**Alternatives Considered**:
- **Avalonia UI**: Cross-platform but adds complexity; Windows-only deployment means WPF is simpler and more mature
- **Windows Forms**: Legacy, limited styling, no XAML data binding, poor developer experience
- **Electron/Web UI**: Massive overhead (100MB+ runtime), slow startup, inappropriate for lightweight monitoring tool
- **Console UI (Spectre.Console)**: Cannot minimize to tray, limited interaction, not suitable for real-time monitoring

**Implementation Notes**:
```xml
<!-- MainWindow.xaml -->
<Window.TaskbarItemInfo>
    <TaskbarItemInfo ProgressState="{Binding ConnectionStatus, Converter={StaticResource StatusToProgressConverter}}" />
</Window.TaskbarItemInfo>
<tb:TaskbarIcon IconSource="/icon.ico" ToolTipText="{Binding StatusText}" />
```

```csharp
// StatusViewModel.cs
public class StatusViewModel : INotifyPropertyChanged
{
    private string _connectionStatus;
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set { _connectionStatus = value; OnPropertyChanged(); }
    }

    public void UpdateFromService()
    {
        // Poll service every 2 seconds
        ConnectionStatus = _apiClient.GetConnectionStatus();
    }
}
```

**References**:
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/
- https://github.com/HavenDV/H.NotifyIcon

---

## R4: HTTP Resilience Pattern (Middleware ’ WebAPI)

**Decision**: HttpClient with Microsoft.Extensions.Http.Resilience standard pipeline

**Rationale**:
- **Standard resilience handler** provides retry (exponential backoff with jitter) + circuit breaker + timeout in single call
- **Retry policy**: 3 attempts with 2s, 4s, 8s delays - aligns with spec requirement (2s, 4s, 8s, 16s, 32s can be configured)
- **Circuit breaker**: Opens after 10 consecutive failures, half-open retry after 60s - prevents cascading failures
- **Timeout**: 30s per request - prevents hung connections
- Integrates seamlessly with IHttpClientFactory and dependency injection

**Alternatives Considered**:
- **Direct Polly policies**: More verbose, requires manual configuration of retry + circuit breaker + timeout policies
- **Manual retry logic**: Error-prone, doesn't handle jitter, no circuit breaker, no observability
- **Refit with Polly**: Adds unnecessary abstraction for simple REST client, overkill for 2-3 endpoints

**Implementation Notes**:
```csharp
builder.Services.AddHttpClient<IMesWebApiClient, MesWebApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["WebApi:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 5;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
});
```

**References**:
- https://learn.microsoft.com/en-us/dotnet/core/resilience/
- https://www.pollydocs.org/strategies/retry.html

---

## R5: Offline Queue Implementation

**Decision**: EF Core 9 + SQLite with Hangfire background jobs

**Rationale**:
- **SQLite** eliminates SQL Server dependency, zero-configuration embedded database, suitable for 10K records (~50MB)
- **EF Core 9** provides type-safe queries, migrations, and LINQ - simpler than raw ADO.NET
- **Hangfire** handles retry scheduling with exponential backoff, built-in dashboard at /hangfire, dead letter queue (Failed state)
- **Background job pattern**: Enqueue upload on WebAPI failure, Hangfire worker dequeues and retries automatically

**Alternatives Considered**:
- **File system queue (JSON files)**: Manual retry logic, no observability, file locking issues, hard to query
- **LiteDB**: Less mature than SQLite, smaller ecosystem, no EF Core integration
- **Quartz.NET**: More complex API than Hangfire, no built-in UI, manual persistence configuration
- **Memory queue (Channel<T>)**: Lost on service restart, doesn't meet "no data loss" requirement

**Implementation Notes**:
```csharp
// Queue on WebAPI failure
try {
    await _webApiClient.UploadInspectionData(data);
} catch (HttpRequestException) {
    await _dbContext.QueuedUploads.AddAsync(new QueuedUpload {
        InspectionDataJson = JsonSerializer.Serialize(data),
        QueuedAt = DateTime.UtcNow,
        RetryCount = 0
    });
    await _dbContext.SaveChangesAsync();
    BackgroundJob.Enqueue<IUploadQueueService>(x => x.RetryUploadAsync(uploadId));
}

// Hangfire worker retries
public async Task RetryUploadAsync(Guid uploadId) {
    var queued = await _dbContext.QueuedUploads.FindAsync(uploadId);
    var data = JsonSerializer.Deserialize<InspectionDataRequest>(queued.InspectionDataJson);
    await _webApiClient.UploadInspectionData(data); // Throws on failure, Hangfire retries
    _dbContext.QueuedUploads.Remove(queued);
    await _dbContext.SaveChangesAsync();
}
```

**References**:
- https://www.hangfire.io/
- https://learn.microsoft.com/en-us/ef/core/providers/sqlite/

---

## R6: Structured Logging Strategy

**Decision**: Serilog with file + console sinks

**Rationale**:
- **Serilog** structured logging captures properties as queryable fields (e.g., `{MachineNumber}`, `{DataSize}`, `{Duration}`)
- **File sink** with rolling intervals (daily) and retention (7 days) prevents disk space exhaustion
- **Console sink** for development debugging (visible when running as console app)
- **Async logging** minimizes performance impact on hot path (shared memory read ’ WebAPI upload)

**Alternatives Considered**:
- **Microsoft.Extensions.Logging only**: Lacks rich sinks (file, seq, elasticsearch), less expressive
- **NLog**: XML configuration less readable than Serilog's fluent API, smaller community
- **Log4Net**: Legacy, no async logging, poor .NET Core integration

**Implementation Notes**:
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/middleware-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .Enrich.WithProperty("Application", "MesMiddleware")
    .Enrich.WithMachineName()
    .CreateLogger();

_logger.LogInformation("Shared memory data received from equipment {MachineNumber}, size {DataSize} bytes, latency {LatencyMs}ms",
    machineNumber, dataSize, latencyMs);
```

**References**:
- https://serilog.net/
- https://github.com/serilog/serilog-sinks-file

---

## R7: Testing Strategy

**Decision**: xUnit + FluentAssertions + WireMock.Net for contract/integration tests

**Rationale**:
- **xUnit** is .NET standard, excellent async support, parameterized tests via `[Theory]`
- **FluentAssertions** improves test readability (`result.Should().BeEquivalentTo(expected)`)
- **WireMock.Net** mocks WebAPI HTTP responses without real server, supports scenario testing (retry logic validation)
- **Shared memory test harness**: Write to MemoryMappedFile from test, verify middleware reads correctly

**Alternatives Considered**:
- **NUnit**: Less idiomatic in .NET Core, xUnit is preferred by .NET team
- **Moq + manual HTTP mocking**: Fragile, doesn't test actual HTTP serialization
- **Testcontainers**: Overkill for middleware service, no Docker needed for MemoryMappedFile tests

**Implementation Notes**:
```csharp
[Fact]
public async Task SharedMemoryMonitor_WhenEquipmentWritesData_DetectsWithin1Second()
{
    // Arrange
    var monitor = new SharedMemoryMonitor(_logger, _options);
    var cts = new CancellationTokenSource();
    var dataReceived = false;
    monitor.OnDataReceived += (sender, data) => { dataReceived = true; };

    // Act
    await monitor.StartAsync(cts.Token);
    SimulateEquipmentWrite("MES_INSPECTION_DATA", testJson); // Test helper writes to MemoryMappedFile + signals EventWaitHandle
    await Task.Delay(TimeSpan.FromMilliseconds(1100)); // Wait 1.1s (should detect within 1s)

    // Assert
    dataReceived.Should().BeTrue();
}
```

**References**:
- https://xunit.net/
- https://fluentassertions.com/
- https://github.com/WireMock-Net/WireMock.Net

---

## Summary of Technology Stack

| Component | Technology | Version | Rationale |
|-----------|------------|---------|-----------|
| Windows Service | .NET BackgroundService | 9.0 | Modern hosting, DI, graceful shutdown |
| Shared Memory IPC | MemoryMappedFile + EventWaitHandle | .NET 9 | Event-driven, <1s latency, Windows-native |
| WPF Desktop UI | WPF MVVM | .NET 9 | Rich desktop, data binding, system tray |
| HTTP Client | HttpClient + Resilience | .NET 9 | Retry, circuit breaker, timeout built-in |
| Offline Queue | EF Core + SQLite + Hangfire | 9.0 / 1.8.x | No SQL Server, background jobs, dashboard |
| Logging | Serilog | 3.x | Structured logs, file/console sinks, async |
| Validation | FluentValidation | 11.x | Type-safe rules, separation of concerns |
| Testing | xUnit + FluentAssertions + WireMock.Net | 2.x | TDD support, HTTP mocking, readable assertions |

All decisions align with .NET 9 best practices, Windows platform requirements, and manufacturing environment reliability needs (offline operation, graceful degradation, comprehensive logging).
