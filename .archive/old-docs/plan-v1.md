# Implementation Plan: Shared Memory Middleware Service

**Branch**: `002-shared-memory-middleware` | **Date**: 2025-11-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-shared-memory-middleware/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Windows middleware service that bridges production equipment with MES WebAPI via shared memory IPC. Equipment writes JSON inspection data to Windows MemoryMappedFile, middleware detects changes via EventWaitHandle callbacks, deserializes data, and POSTs to WebAPI with retry/queue logic. WPF desktop application provides real-time monitoring (connection status, upload history, shared memory activity) with system tray support. Supports bidirectional communication for remote equipment commands. Built with .NET 9 Windows Service + WPF, MemoryMappedFile/Mutex for shared memory, HttpClient with Polly resilience, and SQLite offline queue.

## Technical Context

**Language/Version**: C# / .NET 9 (Windows Service + WPF Desktop)
**Primary Dependencies**:
- .NET 9 Windows Service (hosted service with BackgroundService)
- WPF .NET 9 (desktop monitoring UI)
- System.IO.MemoryMappedFiles (Windows shared memory IPC)
- System.Threading.Mutex/EventWaitHandle (inter-process synchronization)
- HttpClient with Microsoft.Extensions.Http.Resilience (WebAPI client with retry/circuit breaker)
- Serilog 3.x (structured logging)
- Entity Framework Core 9 + SQLite (offline upload queue)
- Hangfire 1.8.x (background job retry processing)
- FluentValidation 11.x (inspection data validation)
- xUnit 2.x + FluentAssertions (testing)

**Storage**:
- SQLite: Offline upload queue persistence (QueuedUploads table)
- MemoryMappedFile: Transient shared memory segments (10MB default, MES_INSPECTION_DATA, MES_EQUIPMENT_CMD)
- appsettings.json: Service configuration (WebAPI URLs, shared memory names/sizes, retry policies)

**Testing**:
- xUnit (unit tests, integration tests, shared memory IPC tests)
- FluentAssertions (assertion library)
- WireMock.Net (mock WebAPI for integration tests)
- MemoryMappedFile test harness (simulate equipment writing to shared memory)

**Target Platform**: Windows 10+ / Windows Server 2019+ (x64)
**Project Type**: Dual project (Windows Service backend + WPF desktop frontend)
**Performance Goals**:
- Shared memory detection latency: <1 second (event-driven, not polling)
- WebAPI upload latency: <3 seconds end-to-end (equipment write → MES confirmed)
- Handle 100 inspections/hour sustained throughput
- WPF UI refresh: every 2 seconds without blocking

**Constraints**:
- Windows-only (MemoryMappedFile, EventWaitHandle, WPF are Windows-specific)
- Single middleware instance per machine (shared memory segment namespace local to machine)
- Must coexist with WebAPI service (feature 001-mes-trace-integration) - middleware calls WebAPI, does NOT replace it
- Graceful degradation: continue queuing data when WebAPI unavailable
- No external message brokers (use SQLite + Hangfire for queue, not RabbitMQ/MSMQ)

**Scale/Scope**:
- 1-5 equipment instances per Windows machine (shared memory segments per equipment)
- 100 inspections/hour per equipment (typical), 500/hour peak
- Shared memory segments: 10MB each (supports ~200 inspection records buffered)
- Offline queue: up to 10,000 records (~50MB SQLite database)
- WPF UI: display last 1000 upload records, last 500 shared memory events

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ⚠️ Principle I: API-First Integration - ADAPTED

**Requirement**: Strictly adhere to Cimforce Trace Management System API specification

**Compliance**:
- ✅ Middleware consumes WebAPI (feature 001) which implements Cimforce API contract
- ✅ Middleware uses same authentication flow (device login → token → accessToken header)
- ✅ Middleware POSTs inspection data via WebAPI POST /api/inspection/upload endpoint
- ⚠️ ADAPTATION: Middleware does NOT directly call Cimforce MES - it calls our WebAPI proxy
- ✅ Equipment JSON format validated against WebAPI InspectionDataRequest schema
- ✅ Retry logic preserves eventual consistency with MES system

**Rationale for Adaptation**: Middleware is a client of our WebAPI (001), not a direct MES integrator. WebAPI handles MES protocol compliance. Middleware focuses on shared memory → HTTP bridge.

**Status**: PASS with adaptation - Principle applies transitively through WebAPI layer

### ✅ Principle II: Token-Based Security

**Requirement**: Token-based authentication for all MES operations

**Compliance**:
- ✅ Middleware obtains token via WebAPI POST /api/auth/login (machine number + IP)
- ✅ All WebAPI requests include accessToken header
- ✅ Token refresh handled automatically (8-hour lifetime)
- ✅ Tokens cached securely in-memory (not logged, not in shared memory)
- ✅ IP address binding enforced by WebAPI (middleware runs on registered machine)

**Status**: PASS - Full security compliance via WebAPI authentication

### ✅ Principle III: Test-First Development (TDD) - Red-Green-Refactor Cycle

**Requirement**: Mandatory Red-Green-Refactor cycle for ALL code

**Compliance**:
- ✅ Contract tests FIRST: Validate shared memory JSON format matches WebAPI schema
- ✅ Integration tests: Mock equipment writing to MemoryMappedFile, verify middleware POSTs to WebAPI
- ✅ Unit tests: SharedMemoryMonitor, JsonDeserializer, RetryQueue, WPF ViewModels
- ✅ Tests written BEFORE implementation (Red phase)
- ✅ Minimum code to pass tests (Green phase)
- ✅ Refactor with passing tests (Refactor phase)

**Testing Strategy**:
1. 🔴 RED: Write test for MemoryMappedFile event detection → Test fails (SharedMemoryMonitor not implemented)
2. 🟢 GREEN: Implement SharedMemoryMonitor with EventWaitHandle callback → Test passes
3. 🔵 REFACTOR: Extract ISharedMemoryMonitor interface, add cancellation token → Tests still pass
4. Repeat for WebAPI client, offline queue, WPF UI

**Status**: PASS - TDD workflow enforced throughout implementation

### ⚠️ Principle IV: Structured Data Contracts - ADAPTED

**Requirement**: Follow three-tier data model (paramData/benchmarks/otherData)

**Compliance**:
- ✅ Equipment JSON in shared memory MUST match three-tier structure
- ✅ Middleware validates paramData, benchmarks, otherData presence via FluentValidation
- ✅ Middleware passes through data to WebAPI without transformation (preserves structure)
- ⚠️ ADAPTATION: Middleware adds envelope metadata (queuedAt, retryCount) for offline queue
- ✅ Dead letter queue preserves original JSON for manual inspection

**Status**: PASS with adaptation - Three-tier structure preserved, minimal envelope added for queue management

### ✅ Principle V: Error Handling & Observability

**Requirement**: Robust error handling and comprehensive logging

**Compliance**:
- ✅ All shared memory operations wrapped in try-catch with detailed logging
- ✅ WebAPI responses validated (success field, code field) with retry on failure
- ✅ Structured logging with Serilog (operation, machineNumber, dataSize, duration, outcome)
- ✅ Performance metrics: shared memory latency, upload success rate, queue depth
- ✅ WPF UI displays errors in real-time (system tray notifications on critical failures)
- ✅ Audit trail: every shared memory read/write logged with timestamp

**Status**: PASS - Comprehensive observability for distributed system debugging

### ⚠️ Principle VI: Cross-Platform Compatibility - DEVIATION

**Requirement**: Windows deployment with path to .NET Core/Linux expansion

**Compliance**:
- ⚠️ DEVIATION: Middleware is Windows-only by design (MemoryMappedFile, EventWaitHandle, WPF)
- ✅ Equipment integration typically Windows-based in manufacturing
- ✅ .NET 9 code is otherwise cross-platform (HttpClient, EF Core, Serilog)
- ⚠️ Linux port would require: replace MemoryMappedFile with POSIX shared memory, replace WPF with Avalonia UI
- ✅ WebAPI (feature 001) already provides cross-platform cloud layer

**Rationale for Deviation**: Shared memory IPC and WPF UI are Windows-specific by nature. Equipment runs on Windows in this deployment. Future Linux support would need architectural change (e.g., gRPC instead of shared memory).

**Status**: DEVIATION JUSTIFIED - Windows-only appropriate for equipment integration layer. Cross-platform handled by WebAPI tier.

### Gate Summary

| Principle | Status | Notes |
|-----------|--------|-------|
| I. API-First Integration | ⚠️ ADAPTED | Via WebAPI proxy, not direct MES |
| II. Token-Based Security | ✅ PASS | Full compliance via WebAPI auth |
| III. Test-First Development | ✅ PASS | TDD Red-Green-Refactor enforced |
| IV. Structured Data Contracts | ⚠️ ADAPTED | Three-tier preserved + queue envelope |
| V. Error Handling & Observability | ✅ PASS | Serilog + WPF monitoring |
| VI. Cross-Platform Compatibility | ⚠️ DEVIATION | Windows-only (justified for equipment layer) |

**Overall Gate Status**: ✅ CONDITIONAL PASS - Proceed to Phase 0 Research

**Justifications Required** (see Complexity Tracking):
1. Windows-only implementation (Principle VI deviation)
2. WebAPI proxy layer instead of direct MES integration (Principle I adaptation)

## Project Structure

### Documentation (this feature)

```text
specs/002-shared-memory-middleware/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── shared-memory-format.json    # Equipment → Middleware JSON schema
│   └── middleware-webapi.http       # Middleware → WebAPI HTTP contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── MesMiddleware.Service/               # Windows Service (equipment bridge)
│   ├── Services/
│   │   ├── SharedMemory/
│   │   │   ├── ISharedMemoryMonitor.cs
│   │   │   ├── SharedMemoryMonitor.cs       # MemoryMappedFile + EventWaitHandle
│   │   │   ├── ISharedMemoryWriter.cs
│   │   │   └── SharedMemoryWriter.cs        # Write commands to equipment
│   │   ├── WebApi/
│   │   │   ├── IMesWebApiClient.cs
│   │   │   ├── MesWebApiClient.cs           # HttpClient to WebAPI (001)
│   │   │   ├── ITokenService.cs
│   │   │   └── TokenService.cs              # Cached auth token management
│   │   ├── Queue/
│   │   │   ├── IUploadQueueService.cs
│   │   │   └── UploadQueueService.cs        # SQLite + Hangfire retry
│   │   └── HostedServices/
│   │       └── MiddlewareHostedService.cs   # BackgroundService main loop
│   ├── Models/
│   │   ├── SharedMemoryMessage.cs           # Equipment JSON envelope
│   │   ├── InspectionDataDto.cs             # WebAPI request DTO
│   │   └── QueuedUpload.cs                  # EF Core entity
│   ├── Data/
│   │   ├── MiddlewareDbContext.cs           # EF Core context
│   │   └── Migrations/
│   ├── Validation/
│   │   └── InspectionDataValidator.cs       # FluentValidation rules
│   ├── appsettings.json
│   ├── Program.cs                           # Windows Service host
│   └── MesMiddleware.Service.csproj
│
├── MesMiddleware.Monitor/                   # WPF Monitoring UI
│   ├── Views/
│   │   ├── MainWindow.xaml                  # Tabbed UI (Status, History, Logs)
│   │   ├── StatusView.xaml                  # Connection status + real-time metrics
│   │   ├── HistoryView.xaml                 # Upload history DataGrid
│   │   └── SharedMemoryView.xaml            # Shared memory activity log
│   ├── ViewModels/
│   │   ├── MainViewModel.cs                 # MVVM pattern
│   │   ├── StatusViewModel.cs
│   │   ├── HistoryViewModel.cs
│   │   └── SharedMemoryViewModel.cs
│   ├── Services/
│   │   ├── IMiddlewareApiClient.cs
│   │   ├── MiddlewareApiClient.cs           # Read service state via HTTP/file
│   │   └── TrayIconService.cs               # System tray management
│   ├── App.xaml
│   ├── App.xaml.cs                          # WPF startup
│   └── MesMiddleware.Monitor.csproj
│
└── MesMiddleware.Shared/                    # Shared models/contracts
    ├── Models/
    │   ├── InspectionRecord.cs
    │   ├── ParamDataItem.cs
    │   ├── BenchmarkItem.cs
    │   └── OtherDataItem.cs
    └── MesMiddleware.Shared.csproj

tests/
├── MesMiddleware.Service.Tests/
│   ├── Contract/
│   │   ├── SharedMemoryFormatTests.cs       # Validate equipment JSON schema
│   │   └── WebApiContractTests.cs           # Validate calls to WebAPI (001)
│   ├── Integration/
│   │   ├── SharedMemoryIpcTests.cs          # Equipment mock → Middleware → WebAPI mock
│   │   ├── OfflineQueueTests.cs             # WebAPI down → queue → recovery
│   │   └── TestServiceFactory.cs            # Test host setup
│   └── Unit/
│       ├── SharedMemoryMonitorTests.cs
│       ├── TokenServiceTests.cs
│       ├── UploadQueueServiceTests.cs
│       └── InspectionDataValidatorTests.cs
└── MesMiddleware.Service.Tests.csproj
```

**Structure Decision**: Dual-project architecture with shared library. MesMiddleware.Service is Windows Service backend (BackgroundService pattern). MesMiddleware.Monitor is WPF desktop UI. MesMiddleware.Shared contains common DTOs/models used by both. This separation allows service to run headless on production machines while operators launch UI on demand for monitoring. Tests focus on shared memory IPC contracts and WebAPI integration.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Windows-only (Principle VI) | Equipment integration requires Windows IPC (MemoryMappedFile), equipment typically Windows-based in manufacturing | POSIX shared memory: Not available on Windows. gRPC/HTTP: Equipment cannot be modified to call HTTP APIs (legacy/third-party equipment). Named pipes: More complex, less performant than MemoryMappedFile for large payloads. |
| WebAPI proxy layer (Principle I) | Equipment cannot directly call Cimforce MES HTTP API (no network stack, no HTTPS, or third-party vendor restrictions) | Equipment → MES direct: Equipment lacks HTTP client capability or is locked by vendor. Middleware embedding MES logic: Violates separation of concerns - WebAPI (001) already implements MES protocol, don't duplicate. |
