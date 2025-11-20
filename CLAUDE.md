# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

MES Middleware Service - A production-grade Windows Service and WPF desktop application that bridges equipment data with the MES cloud system via HTTP REST API. Built with .NET 9 following strict Test-Driven Development (TDD) practices.

**Architecture Version**: v2.0 (Web API) - Migrated from v1.0 (Shared Memory IPC)

## Technology Stack

**Core Platform**: .NET 9
- **Backend**: ASP.NET Core Web API + Windows Service (Kestrel embedded web server)
- **Frontend**: WPF Desktop Application (net9.0-windows)
- **Testing**: xUnit 2.x + FluentAssertions + Moq

**Key Dependencies**:
- ASP.NET Core 9 (Web API, Controllers, Middleware)
- Kestrel (embedded web server)
- System.Threading.Channels (bounded in-memory queue)
- Entity Framework Core 9 + SQLite (offline queue persistence)
- Hangfire 1.8.x (background job retry processing)
- FluentValidation 11.x (HTTP request validation)
- Serilog 3.x (structured logging)
- Swashbuckle.AspNetCore 10.x (Swagger/OpenAPI documentation)
- CommunityToolkit.Mvvm (MVVM framework for WPF)

## Solution Structure

```
MesMiddleware.sln
├── src/
│   ├── MesMiddleware.Service/          # Windows Service (backend)
│   │   ├── Controllers/                # ASP.NET Core Controllers
│   │   │   ├── InspectionController.cs # POST /api/inspection/submit
│   │   │   └── StatusController.cs     # GET /api/status, /api/status/history
│   │   ├── Services/
│   │   │   ├── HostedServices/         # BackgroundService implementations
│   │   │   │   └── InspectionChannelProcessor.cs  # Processes Channel queue
│   │   │   ├── WebApi/                 # MES Cloud API client + JWT
│   │   │   └── Queue/                  # SQLite offline queue + Hangfire
│   │   ├── Data/                       # EF Core DbContext + Migrations
│   │   ├── Models/                     # Domain entities
│   │   ├── Validation/                 # FluentValidation validators
│   │   └── Program.cs                  # ASP.NET Core WebApplication entry
│   ├── MesMiddleware.Monitor/          # WPF desktop monitoring UI
│   │   ├── ViewModels/                 # MVVM ViewModels
│   │   ├── Views/                      # XAML views
│   │   ├── Services/                   # HTTP API client (polling)
│   │   └── Resources/                  # i18n resources (zh-TW, zh-CN, en)
│   └── MesMiddleware.Shared/           # Shared models and contracts
├── tests/
│   ├── MesMiddleware.Service.Tests/
│   │   ├── Contract/                   # JSON schema validation tests (5 tests)
│   │   ├── Integration/                # HTTP endpoint, database tests (19 tests)
│   │   └── Unit/                       # Service logic, validators (30 tests)
│   └── MesMiddleware.Monitor.Tests/
│       └── Unit/                       # ViewModel unit tests (26 tests)
├── .archive/                           # Archived files
│   ├── SharedMemory/                   # v1.0 shared memory code (removed)
│   └── old-docs/                       # v1.0 documentation (archived)
├── SRS.md                              # Software Requirements Specification (main doc)
├── COVERAGE_ANALYSIS.md                # Test coverage analysis
└── README.md                           # Quick start guide
```

## Commands

### Build and Test

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Build for release
dotnet build -c Release

# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~SharedMemoryIpcTests"

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Database Management (SQLite)

```bash
# Navigate to service project
cd src/MesMiddleware.Service

# Add new migration
dotnet ef migrations add MigrationName

# Update database schema
dotnet ef database update

# Drop database (dev only)
dotnet ef database drop
```

### Service Deployment

```bash
# Publish Windows Service (self-contained)
dotnet publish src/MesMiddleware.Service -c Release -r win-x64 --self-contained

# Install Windows Service (requires Administrator)
.\install-service.ps1

# Start/stop service
Start-Service -Name MesMiddlewareService
Stop-Service -Name MesMiddlewareService
```

### Run Locally (Development)

```bash
# Run Windows Service in console mode
cd src/MesMiddleware.Service
dotnet run

# Run WPF Monitor application
cd src/MesMiddleware.Monitor
dotnet run
```

## Architecture

### HTTP REST API Protocol

The system uses standard HTTP REST API for equipment-to-middleware communication:

**Data Flow (Equipment → Middleware → MES Cloud)**:
1. Equipment POSTs JSON to `http://localhost:5100/api/inspection/submit`
2. Middleware validates request (FluentValidation)
3. Data written to Channel<InspectionRecord> (Tier 1: in-memory queue, capacity 1000)
4. InspectionChannelProcessor reads from channel and uploads to MES Cloud API
5. On success: Log statistics (TotalReceived++, SuccessfulUploads++)
6. On failure: Save to SQLite queue (Tier 2: persistent storage)
7. Hangfire schedules retry job with exponential backoff (Tier 3: 2s, 4s, 8s, 16s, 32s)

**Three-Tier Queue Architecture**:
- **Tier 1**: Channel<InspectionRecord> - High-speed in-memory buffer (1000 items, BoundedChannelFullMode.Wait)
- **Tier 2**: SQLite UploadQueue - Persistent offline storage
- **Tier 3**: Hangfire - Background job retry with exponential backoff (max 5 retries)

### Service Architecture

**Windows Service (MesMiddleware.Service)**:
- `InspectionController` - POST /api/inspection/submit endpoint
- `StatusController` - GET /api/status, /api/status/history endpoints
- `InspectionChannelProcessor` - BackgroundService reading from Channel queue
- `MesWebApiClient` - MES Cloud API client with JWT auth + Polly retry
- `UploadQueueService` - SQLite queue management + Hangfire job scheduling
- `TokenService` - JWT token caching and automatic refresh

**WPF Monitor (MesMiddleware.Monitor)**:
- MVVM architecture with CommunityToolkit.Mvvm
- `MiddlewareApiClient` - HTTP polling client (polls GET /api/status every 2 seconds)
- `StatusViewModel` - Connection status indicators (Green/Yellow/Red)
- `HistoryViewModel` - Upload history display (last 100 records)
- `TrayIconService` - System tray integration with balloon notifications
- Multi-language support (zh-TW, zh-CN, en) via .resx files

## Test-Driven Development (TDD)

**CRITICAL**: This project follows strict Red-Green-Refactor cycles. ALL code must be preceded by failing tests.

### Test Coverage Status (see COVERAGE_ANALYSIS.md)

**Total Tests**: 80 tests (100% passing)
- Service Tests: 54 tests (Contract: 5, Integration: 19, Unit: 30)
- Monitor Tests: 26 tests (StatusViewModel: 6, HistoryViewModel: 8, TrayIconService: 12)

**Implementation Status**:
- ✅ User Story 1 (Equipment Data Collection): 100% complete, 54 tests
- ✅ User Story 2 (Real-Time Monitoring Dashboard): 100% complete, 26 tests
- ❌ User Story 3 (Bidirectional Commands): 0% complete, 0 tests

### TDD Workflow

When adding new features:

1. **RED PHASE**: Write failing tests first
   - Contract tests: Validate JSON schemas and data structures
   - Integration tests: Test IPC, database, HTTP interactions
   - Unit tests: Test service logic in isolation

2. **GREEN PHASE**: Write minimal code to pass tests
   - Implement service interfaces
   - Wire up dependency injection
   - Ensure all tests pass

3. **REFACTOR PHASE**: Improve code quality
   - Extract interfaces for testability
   - Add error handling and logging
   - Optimize performance

### Running Tests for Requirement Verification

The `COVERAGE_ANALYSIS.md` file provides ultra-thorough requirement-to-test mapping. To verify requirements:

```bash
# Run all tests to verify implemented features
dotnet test

# Verify specific user story coverage
dotnet test --filter "FullyQualifiedName~InspectionEndpointTests"  # US1: HTTP API Endpoint
dotnet test --filter "FullyQualifiedName~WebApiUploadTests"        # US1: MES Cloud Upload
dotnet test --filter "FullyQualifiedName~OfflineQueueTests"        # US1: Offline Queue
dotnet test --filter "FullyQualifiedName~StatusViewModelTests"     # US2: WPF Status UI
dotnet test --filter "FullyQualifiedName~HistoryViewModelTests"    # US2: WPF History UI

# Check test output against functional requirements in COVERAGE_ANALYSIS.md
```

**Yes, the coverage analysis can continue performing requirement verification** - the 80 existing tests provide 100% coverage of implemented features (US1 + US2). User Story 3 requires 14 additional tasks (T056-T069) with an estimated 15-20 new tests.

## Configuration

### Service Configuration (appsettings.json)

```json
{
  "Urls": "http://localhost:5100",
  "WebApi": {
    "BaseUrl": "https://your-mes-api.com",
    "Username": "middleware_user",
    "Password": "your_password",
    "MachineNumber": "MACHINE-01",
    "MachineIp": "192.168.1.100"
  },
  "InspectionChannel": {
    "Capacity": 1000,
    "FullMode": "Wait"
  },
  "Queue": {
    "DatabasePath": "Data/queue.db",
    "MaxRetries": 5,
    "RetryDelaySeconds": 2
  }
}
```

### Multi-Language Support

WPF Monitor supports runtime language switching:
- **zh-TW** (繁體中文) - Default
- **zh-CN** (简体中文)
- **en** (English)

Language resources are in `src/MesMiddleware.Monitor/Resources/Strings.*.resx`

## Logging

Logs are written to `logs/` directory with daily rotation:
- Service: `logs/middleware-service-{Date}.log`
- Monitor: `logs/middleware-monitor-{Date}.log`
- Retention: 7 days (configurable in appsettings.json)

View recent logs:
```bash
Get-Content logs\middleware-service-*.log -Tail 50
```

## Health Checks

The service exposes a health check endpoint (see `HealthChecks/` folder):
```bash
curl http://localhost:5000/health
```

Hangfire dashboard for background job monitoring:
```
http://localhost:5100/hangfire
```

Swagger/OpenAPI interactive documentation:
```
http://localhost:5100/swagger
```

## Common Development Tasks

### Adding a New HTTP API Endpoint

1. Create controller in `Controllers/` (e.g., `NewFeatureController.cs`)
2. Write contract tests for request/response models
3. Implement FluentValidation validator for request DTO
4. Add controller action method (e.g., POST /api/newfeature)
5. Write integration tests with WebApplicationFactory
6. Update Swagger documentation attributes
7. Update DI registration in `Program.cs` if needed

### Adding a New MES Cloud API Call

1. Add endpoint method to `IMesWebApiClient` interface
2. Write integration tests with WireMock.Net
3. Implement method in `MesWebApiClient.cs`
4. Add authentication/retry logic (Polly policies)
5. Update DI registration in `Program.cs`

### Adding a New WPF View

1. Create ViewModel in `ViewModels/` (inherit from `ObservableObject`)
2. Write unit tests for ViewModel properties and commands
3. Create XAML view in `Views/`
4. Add localization strings to `Resources/Strings.*.resx`
5. Register ViewModel in DI (`App.xaml.cs`)

## Known Limitations

1. **Windows-only**: WPF is Windows-specific (backend HTTP API is cross-platform compatible)
2. **Single instance**: One middleware service per machine (localhost HTTP endpoint)
3. **User Story 3 not implemented**: Bidirectional command control is deferred (requires WebSocket/SSE for real-time bidirectional communication)
4. **SQLite concurrency**: Consider SQL Server/PostgreSQL for production if high concurrent access is needed

## Documentation

**Primary Documentation**:
- **SRS.md** ⭐ - Software Requirements Specification (200+ pages):
  - Complete functional requirements (FR-001 to FR-018)
  - Non-functional requirements (NFR-001 to NFR-017)
  - Full API specification (Swagger/OpenAPI format)
  - Data models with validation rules
  - Deployment guide (installation, upgrade, rollback)
  - Migration guide (v1.0 Shared Memory → v2.0 Web API)
  - Equipment integration examples (C#, LabVIEW)
- **README.md** - Quick start guide with system overview
- **COVERAGE_ANALYSIS.md** - Test-to-requirement mapping (80 tests, 85% coverage)
- **CLAUDE.md** - This file (Claude Code project guidance)

**Archived Documentation** (v1.0):
- **.archive/old-docs/** - Old shared memory architecture specs
  - `spec-v1-shared-memory.md`: Original functional requirements
  - `plan-v1.md`: Original implementation plan
  - `tasks-v1.md`: Original task breakdown (79 tasks)
  - `data-model-v1.md`: Original database schema

## Performance Metrics

- HTTP endpoint response time: < 100ms p95 (target: < 200ms)
- Channel write latency: < 1ms p99 (target: < 5ms)
- MES Cloud API upload latency: < 500ms (target: < 2s)
- WPF status polling latency: < 50ms p95 (target: < 100ms)
- Test execution time: 1-2 seconds (target: < 5s)

## Troubleshooting

### Service Won't Start
- Check Windows Event Viewer: `Application and Services Logs > MesMiddleware`
- Verify .NET 9 runtime is installed
- Check firewall for port 5100 (HTTP endpoint)
- Verify port 5100 is not already in use: `netstat -ano | findstr :5100`

### HTTP API Endpoint Errors
- Test endpoint: `curl http://localhost:5100/health`
- Check Swagger UI: `http://localhost:5100/swagger`
- Review service logs: `logs/middleware-service-*.log`
- Verify JSON payload matches InspectionRecord schema

### MES Cloud API Connection Failures
- Verify `appsettings.json` WebApi.BaseUrl
- Test network connectivity: `ping your-mes-api-domain.com`
- Check JWT credentials are correct
- Review MES Cloud API logs for authentication errors
- Check Hangfire dashboard for retry status: `http://localhost:5100/hangfire`

### Database Locked (SQLite)
- Stop WPF Monitor (it shares the same SQLite database)
- Restart service
- For production, use SQL Server/PostgreSQL instead

## Code Style

Follow standard .NET conventions:
- Use C# 12 features (implicit usings, file-scoped namespaces, required properties)
- Async suffix for async methods (`LoadHistoryAsync`, `UploadAsync`)
- Interface naming: `IServiceName`
- Private fields: `_camelCase`
- Public properties: `PascalCase`
- Test naming: `MethodName_Scenario_ExpectedBehavior`

## Git Workflow

Commit message format (conventional commits):
```
feat: Add command timeout handling
fix: Resolve database lock issue
test: Add shared memory integration tests
docs: Update README configuration section
```

All PRs require:
- Passing tests (`dotnet test`)
- TDD compliance (tests before implementation)
- At least 1 code review approval
