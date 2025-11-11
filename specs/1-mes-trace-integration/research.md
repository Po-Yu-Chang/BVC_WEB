# Technology Research: MES Trace Data Integration

**Feature**: MES Trace Data Integration Web API
**Date**: 2025-11-11
**Purpose**: Document technology choices, rationale, and alternatives for .NET 9 ASP.NET Core Web API implementation

## Executive Summary

This research documents comprehensive technology decisions for implementing a production-grade .NET 9 ASP.NET Core Web API for MES integration. Key decisions prioritize modern .NET patterns: Microsoft.Extensions.Http.Resilience for HTTP resilience, IMemoryCache with SemaphoreSlim for token management, Hangfire + SQLite for offline queuing, Serilog for structured logging, FluentValidation for complex object validation, and WireMock.Net + WebApplicationFactory for integration testing. The approach balances modern best practices with manufacturing environment requirements for reliability, observability, and maintainability.

---

## R1: HTTP Resilience Pattern

### Decision

**Microsoft.Extensions.Http.Resilience** with standard resilience pipeline

### Rationale

Microsoft.Extensions.Http.Resilience is the official successor to Microsoft.Extensions.Http.Polly and is specifically designed for .NET 8/9 HttpClient scenarios. It provides HTTP-specific APIs that integrate Polly v8 with IHttpClientFactory using pre-configured resilience pipelines. The AddStandardResilienceHandler method includes retry with exponential backoff, circuit breaker, rate limiter, and total request timeout in a single call, eliminating manual policy configuration. This approach is production-tested and recommended by Microsoft for all new .NET projects.

### Alternatives Considered

**Polly directly**: Requires manual policy configuration and lacks HTTP-specific optimizations provided by the resilience package. More verbose setup compared to standard handlers.

**Manual retry logic**: Reinvents tested patterns, harder to maintain, lacks features like circuit breaker and jitter. Error-prone for edge cases like transient failures.

**Refit with Polly**: Adds abstraction overhead for simple HTTP calls. The MES API integration is straightforward enough that typed client wrappers provide minimal benefit.

### Implementation Notes

```csharp
// Install NuGet package
// Microsoft.Extensions.Http.Resilience version 9.10.0

// Configure in Program.cs
builder.Services.AddHttpClient<IMesApiClient, MesApiClient>(client =>
{
    client.BaseAddress = new Uri(settings.MesApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true; // Prevent thundering herd
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});
```

Standard resilience pipeline includes: Retry (exponential backoff with jitter), Circuit Breaker (opens on repeated failures), Rate Limiter (prevents overload), Timeout policies (per-attempt and total request). Jitter prevents synchronized retries from multiple clients.

### References

- Microsoft Learn: Build resilient HTTP apps https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience
- NuGet: Microsoft.Extensions.Http.Resilience 9.10.0
- Microsoft DevBlogs: Building Resilient Cloud Services with .NET 8

---

## R2: Token Management Pattern

### Decision

**IMemoryCache with SemaphoreSlim** for thread-safe token caching

### Rationale

IMemoryCache is thread-safe for basic get/set operations but requires additional synchronization to prevent multiple concurrent threads from triggering expensive token refresh calls simultaneously. The SemaphoreSlim pattern ensures only one thread executes the token refresh logic when cache misses occur, while other threads wait for the result. This double-check locking pattern prevents the thundering herd problem where multiple device authentication requests could trigger redundant MES API login calls. For single-server deployment scenarios, in-memory caching provides microsecond-level access times without network I/O overhead.

### Alternatives Considered

**IDistributedCache with Redis**: Requires separate Redis service installation and introduces network I/O latency (milliseconds vs microseconds). Beneficial for multi-server web farms but unnecessary for single-instance MES integration where one Web API serves one blind hole detection machine.

**Database-backed tokens**: Adds database queries to every authenticated request. SQLite or SQL Server introduces persistent storage overhead when tokens are ephemeral (valid for hours, not days). Complicates token expiration logic.

**Memory-only without synchronization**: Risk of race conditions where multiple threads refresh token simultaneously, wasting MES API calls and potentially hitting rate limits. IMemoryCache alone doesn't prevent factory method from running multiple times on concurrent cache misses.

### Implementation Notes

```csharp
// Token caching service with SemaphoreSlim
public class TokenCacheService
{
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<string> GetOrRefreshTokenAsync(string machineNumber, string ipAddress)
    {
        var cacheKey = $"token_{machineNumber}";

        // Fast path: token in cache
        if (_cache.TryGetValue(cacheKey, out string cachedToken))
            return cachedToken;

        // Slow path: acquire lock for token refresh
        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock (another thread may have refreshed)
            if (_cache.TryGetValue(cacheKey, out cachedToken))
                return cachedToken;

            // Call MES API login endpoint
            var newToken = await _mesApiClient.LoginAsync(machineNumber, ipAddress);

            // Cache with absolute expiration (token lifetime from API)
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromHours(8))
                .SetPriority(CacheItemPriority.High);

            _cache.Set(cacheKey, newToken, cacheOptions);
            return newToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

Key considerations: Use SemaphoreSlim (not lock keyword) for async compatibility. Double-check pattern prevents redundant API calls after lock acquisition. SetPriority ensures tokens aren't evicted under memory pressure. Monitor cache hit/miss ratio for performance validation.

### References

- Microsoft Learn: Cache in-memory in ASP.NET Core
- Scott Hanselman: Eyes Wide Open - Correct Caching is Always Hard
- Stack Overflow: MemoryCache Thread Safety discussion

---

## R3: Offline Queue Implementation

### Decision

**Hangfire 1.8.x + SQLite** for background job processing and retry queue

### Rationale

Hangfire provides automatic retry logic with exponential backoff, built-in dashboard for monitoring failed jobs, and persistent job storage without requiring separate message broker infrastructure. SQLite integration eliminates the need for SQL Server or PostgreSQL installation in manufacturing environments. Hangfire's AutomaticRetryAttribute handles transient failures automatically and moves jobs to Failed state after retry exhaustion, providing dead letter queue semantics. The dashboard UI allows operators to manually requeue failed jobs without developer intervention, critical for manufacturing support scenarios.

### Alternatives Considered

**Quartz.NET**: More flexible scheduling but requires manual retry implementation and lacks built-in monitoring dashboard. Better for complex cron-based scheduling not needed for this use case where jobs are triggered by inspection completion events.

**BackgroundService with file-based queue**: Simpler implementation but requires custom retry logic, no monitoring UI, and manual dead letter handling. Suitable for very simple scenarios but lacks observability needed for production support.

**Azure Storage Queue**: Cloud dependency incompatible with on-premises manufacturing environment. Introduces external service dependency and requires internet connectivity.

**RabbitMQ/MSMQ**: Message broker overhead for single-instance integration. Requires separate service installation and management. Better suited for distributed systems with multiple consumers.

### Implementation Notes

```csharp
// Install NuGet packages
// Hangfire.Core 1.8.x
// Hangfire.AspNetCore 1.8.x
// Hangfire.SQLite (unofficial but stable)

// Configure in Program.cs
builder.Services.AddHangfire(config => config
    .UseSQLiteStorage("Data Source=C:\\ProgramData\\MesTraceIntegration\\hangfire.db")
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings());

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1; // Single-threaded processing for sequential uploads
    options.SchedulePollingInterval = TimeSpan.FromMinutes(1);
});

// Enqueue background job
[AutomaticRetry(Attempts = 5, DelayInSecondsByAttemptFunc = attempt => (int)Math.Pow(2, attempt))]
public async Task UploadInspectionDataAsync(InspectionRecord record)
{
    await _mesApiClient.UploadDataAsync(record);
}

// Enqueue from controller
BackgroundJob.Enqueue<IMesUploadService>(x => x.UploadInspectionDataAsync(record));
```

Retry behavior: 5 attempts with exponential backoff (2s, 4s, 8s, 16s, 32s). Failed jobs visible in dashboard at /hangfire. SQLite storage persists jobs across application restarts. Dead letter handling: Jobs remain in Failed state for manual investigation and requeue.

### References

- Hangfire Documentation: Dealing with Exceptions
- Hangfire 1.8.0 Release Notes
- Medium: Background Job Scheduling using Hangfire

---

## R4: Structured Logging Strategy

### Decision

**Serilog 3.x** with file and console sinks

### Rationale

Serilog provides rich structured logging capabilities beyond Microsoft.Extensions.Logging's default providers, including automatic property capture, asynchronous logging for minimal performance impact, and flexible sink configuration. Structured logs enable querying by specific fields (machine number, trace code, operation type) rather than regex parsing text logs. Serilog's file sink with rolling policies automatically manages log retention without manual cleanup scripts. Integration with ASP.NET Core 9 is seamless via UseSerilog() in Program.cs, and Serilog reads configuration from appsettings.json for environment-specific log levels.

### Alternatives Considered

**Microsoft.Extensions.Logging only**: Adequate for console/debug output but lacks advanced sinks like rolling file, Seq, or Elasticsearch. Structured logging support requires more manual configuration.

**NLog**: Comparable features to Serilog but less idiomatic in modern .NET ecosystem. Configuration through XML instead of JSON. Smaller community and fewer integrations.

**Log4Net**: Legacy library, less performant than Serilog, lacks async logging. Not actively developed compared to Serilog which receives regular updates for new .NET versions.

### Implementation Notes

```csharp
// Install NuGet packages
// Serilog.AspNetCore 8.0+
// Serilog.Sinks.File 5.0+
// Serilog.Sinks.Console 5.0+

// Configure in Program.cs
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "C:\\ProgramData\\MesTraceIntegration\\logs\\log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

// Structured logging usage
_logger.LogInformation(
    "MES data upload completed. Machine={MachineNumber}, TraceCode={TraceCode}, Duration={DurationMs}ms, StatusCode={StatusCode}",
    machineNumber, traceCode, duration.TotalMilliseconds, statusCode);
```

Best practices: Avoid logging sensitive data (passwords, tokens). Use @ operator for complex object serialization. Enable async logging in production for performance. Configure different log levels per namespace (Debug for application, Warning for Microsoft.AspNetCore). Use log scopes for correlation IDs.

### References

- Serilog Official Documentation
- Milan Jovanovic: 5 Serilog Best Practices for Better Structured Logging
- CodeWithMukesh: Structured Logging with Serilog in ASP.NET Core

---

## R5: Input Validation Strategy

### Decision

**FluentValidation 11.x** with ASP.NET Core dependency injection

### Rationale

FluentValidation provides expressive, strongly-typed validation rules that are easier to read and maintain than data annotations. The library excels at validating complex nested objects like the three-tier MES data structure (paramData/benchmarks/otherData), using SetValidator for child validators and RuleForEach for arrays. Dependency injection integration allows validators to access services like IConfiguration for environment-specific rules. FluentValidation separates validation logic from domain models, adhering to single responsibility principle and enabling reusable validation rules across multiple endpoints.

### Alternatives Considered

**Data Annotations**: Built-in but limited expressiveness for complex rules. Validation logic coupled to model classes. Difficult to validate conditional rules or cross-property dependencies.

**Manual validation in controllers**: Scatters validation logic across application, duplicates code, harder to test in isolation. Violates DRY principle.

**Custom validation attributes**: Requires writing boilerplate attribute classes. Less discoverable than FluentValidation's fluent API. Testing custom attributes is cumbersome.

### Implementation Notes

```csharp
// Install NuGet package
// FluentValidation.AspNetCore 11.3.0
// FluentValidation.DependencyInjectionExtensions 11.3.0

// Register in Program.cs
builder.Services.AddValidatorsFromAssemblyContaining<InspectionRecordValidator>();
builder.Services.AddFluentValidationAutoValidation();

// Root validator
public class InspectionRecordValidator : AbstractValidator<InspectionRecord>
{
    public InspectionRecordValidator(IValidator<ParamData> paramValidator, IValidator<BenchmarkData> benchmarkValidator)
    {
        RuleFor(x => x.TraceCode)
            .NotEmpty().When(x => string.IsNullOrEmpty(x.LotNo))
            .WithMessage("Either TraceCode or LotNo is required");

        RuleFor(x => x.ParamData).SetValidator(paramValidator);
        RuleForEach(x => x.Benchmarks).SetValidator(benchmarkValidator);
    }
}

// Nested validator
public class ParamDataValidator : AbstractValidator<ParamData>
{
    public ParamDataValidator()
    {
        RuleFor(x => x.MaxHoleDiameter).GreaterThan(0).WithMessage("Diameter must be positive");
        RuleFor(x => x.MinHoleDiameter).LessThan(x => x.MaxHoleDiameter)
            .WithMessage("Min diameter must be less than max diameter");
    }
}
```

Validators are registered as Transient (safest for DI). Automatic validation triggers before controller action executes. ValidationResult provides detailed error messages for API responses. Validators can inject services for database lookups or configuration-based rules.

### References

- FluentValidation Official Documentation: ASP.NET Core Integration
- FluentValidation Official Documentation: Dependency Injection
- Medium: Fluent Validation with .NET Core

---

## R6: Testing Strategy

### Decision

**xUnit + FluentAssertions + WireMock.Net + Microsoft.AspNetCore.Mvc.Testing**

### Rationale

xUnit is the de facto standard for modern .NET testing with async-first design and parallel test execution. FluentAssertions provides readable assertion syntax that generates clear failure messages. WireMock.Net eliminates dependency on live MES API for integration tests by simulating HTTP responses with configurable stubs. Microsoft.AspNetCore.Mvc.Testing's WebApplicationFactory bootstraps the entire ASP.NET Core pipeline in-memory, enabling true integration tests without deployment. This combination supports contract tests (validate exact JSON structures), integration tests (end-to-end flows with mocked HTTP), and unit tests (business logic isolation).

### Alternatives Considered

**Moq + RestSharp mocks**: Manual HTTP mocking is fragile and doesn't test actual HTTP serialization. WireMock.Net provides more realistic HTTP simulation including headers, status codes, and latency.

**NUnit**: Less idiomatic for modern .NET projects. xUnit's constructor/dispose pattern for test fixtures is cleaner than NUnit's Setup/TearDown attributes.

**Testcontainers for MES API**: Running containerized MES system adds infrastructure complexity and test execution time. WireMock provides faster, more deterministic HTTP mocking.

**Manual testing only**: Violates TDD requirements, no regression protection, time-consuming for repeated validation cycles during development.

### Implementation Notes

```csharp
// Install NuGet packages
// xUnit 2.6+
// FluentAssertions 6.12+
// WireMock.Net 1.5+
// Microsoft.AspNetCore.Mvc.Testing 9.0+

// Integration test with WebApplicationFactory
public class MesApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly WireMockServer _wireMock;

    public MesApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace MES API client with WireMock endpoint
                services.Configure<MesApiSettings>(s => s.BaseUrl = _wireMock.Url);
            });
        });
    }

    [Fact]
    public async Task UploadData_Should_Retry_On_Transient_Failure()
    {
        // Arrange: WireMock returns 503, then 200
        _wireMock.Given(Request.Create().WithPath("/api/v1/MesTrace/TraceData/AddData3"))
            .InScenario("retry")
            .WillSetStateTo("success")
            .RespondWith(Response.Create().WithStatusCode(503));

        _wireMock.Given(Request.Create().WithPath("/api/v1/MesTrace/TraceData/AddData3"))
            .InScenario("retry")
            .WhenStateIs("success")
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { success = true }));

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/inspection/upload", testData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _wireMock.LogEntries.Should().HaveCount(2); // Verify retry occurred
    }
}
```

Best practices: Use IClassFixture for shared WebApplicationFactory. CollectionFixture for sharing WireMock server across test classes. Test both happy path and error scenarios. Verify retry behavior with WireMock scenarios. Name tests clearly (Given_When_Then pattern).

### References

- Microsoft Learn: Integration Tests in ASP.NET Core
- Code Maze: Integration Testing with WireMock.NET
- Anton DevTips: ASP.NET Core Integration Testing Best Practices

---

## R7: .NET Version Selection

### Decision

**.NET 9.0** as primary target

### Rationale

.NET 9 is the latest LTS release (supported until November 2027) providing the most recent performance improvements, security patches, and language features. Microsoft.Extensions.Http.Resilience 9.10.0 is optimized for .NET 9 with enhanced resilience patterns. Hangfire, Serilog, FluentValidation, and all selected libraries have confirmed .NET 9 compatibility. Starting new projects on the latest LTS version ensures maximum support window and access to modern framework features like improved JSON serialization and LINQ optimizations.

### Alternatives Considered

**.NET 8.0 (LTS)**: Previous LTS version, supported until November 2026. Valid choice but shorter support window than .NET 9. Missing latest performance improvements.

**.NET 6.0**: End of support November 2024. Using a framework at end-of-life introduces security risks and prevents access to modern library versions.

**.NET Framework 4.8**: Legacy, Windows-only, incompatible with cross-platform requirements. No support for modern resilience libraries or async/await optimizations.

### Implementation Notes

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

Use C# 13 features: file-scoped namespaces, global usings, record types for DTOs. Enable nullable reference types for compile-time null safety. Target win-x64 runtime identifier for self-contained deployment.

### References

- Microsoft .NET 9 Release Notes
- .NET Support Policy

---

## R8: JSON Serialization

### Decision

**System.Text.Json** (built-in .NET serialization)

### Rationale

System.Text.Json is the default serializer in .NET 9 with superior performance compared to Newtonsoft.Json. Built-in support for source generators enables zero-allocation deserialization. Native async streaming for large payloads. Exact property name matching aligns with MES API contract requirements (PrtMacNo, accessToken case-sensitive fields). No external dependencies reduces package management overhead.

### Alternatives Considered

**Newtonsoft.Json (Json.NET)**: Legacy choice, slower than System.Text.Json in benchmarks. Still maintained but not recommended for new .NET projects.

**MessagePack**: Binary serialization format incompatible with JSON-based MES API specification.

### Implementation Notes

```csharp
services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = false; // Strict matching
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
```

Configuration: Case-sensitive matching per API spec. Omit null fields for cleaner payloads. Serialize enums as strings for readability.

---

## Summary of Technology Stack

| Component | Technology | Version | Rationale |
|-----------|-----------|---------|-----------|
| Framework | .NET | 9.0 | Latest LTS, modern features, long support window |
| HTTP Resilience | Microsoft.Extensions.Http.Resilience | 9.10.0 | Official pattern, pre-configured pipelines, Polly v8 integration |
| Token Caching | IMemoryCache + SemaphoreSlim | Built-in | Thread-safe refresh, low latency, single-instance optimization |
| Offline Queue | Hangfire + SQLite | 1.8.x | Automatic retry, monitoring dashboard, persistent storage |
| Structured Logging | Serilog | 3.x | Async logging, structured data, rich sink ecosystem |
| Input Validation | FluentValidation | 11.x | Expressive rules, nested object support, DI integration |
| Testing Framework | xUnit + FluentAssertions | 2.6+, 6.12+ | Modern .NET standard, readable assertions |
| HTTP Mocking | WireMock.Net | 1.5+ | Realistic HTTP simulation, scenario support |
| Integration Testing | Microsoft.AspNetCore.Mvc.Testing | 9.0+ | In-memory pipeline, WebApplicationFactory pattern |
| JSON Serialization | System.Text.Json | Built-in | Performance, native async, API contract alignment |

---

## Risk Assessment & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Network instability | High | High | Microsoft.Extensions.Http.Resilience with retry/circuit breaker/timeout + Hangfire offline queue |
| MES API breaking changes | Medium | High | Contract tests with WireMock validate exact JSON structures, fail fast on schema changes |
| Token expiration mid-request | Low | Medium | SemaphoreSlim prevents race conditions, 401 response triggers automatic re-authentication |
| Hangfire job deadlock | Low | Medium | Single WorkerCount=1 ensures sequential processing, SQLite WAL mode for concurrent reads |
| SQLite database corruption | Low | High | Regular backups, atomic writes, SQLite auto-recovery mechanisms |
| Log file disk space exhaustion | Medium | Low | Serilog rolling file sink with 7-day retention, monitor disk space via Windows alerts |

---

## Next Steps

1. Create project structure with .NET 9 Web API template
2. Install NuGet packages per technology decisions above
3. Implement authentication with token caching (R2)
4. Configure HTTP resilience pipeline (R1)
5. Set up Hangfire background jobs (R3)
6. Configure Serilog structured logging (R4)
7. Implement FluentValidation rules (R5)
8. Write contract tests with WireMock.Net (R6)

---

**Research Approval**: Technology choices align with .NET 9 best practices, manufacturing reliability requirements, and modern observability standards. All selected libraries are production-proven with active maintenance and strong community support.
