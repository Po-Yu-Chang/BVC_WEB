# Implementation Plan: MES Trace Data Integration

**Branch**: `1-mes-trace-integration` | **Date**: 2025-11-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/1-mes-trace-integration/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Implement secure MES integration Web API for blind hole detection and inspection equipment to authenticate with Cimforce Trace Management System and upload production quality data (inspection results, defect coordinates, test parameters). The API enables automated traceability by handling device login with IP-bound token authentication, structured three-tier data uploads (paramData/benchmarks/otherData), and optional batch validation to prevent mixed-lot errors. Built with ASP.NET Core Web API on Windows to support multiple equipment types (AOI, AVI, VRS, ET, blind hole detection) with retry logic, offline queueing, and comprehensive audit logging.

## Technical Context

**Language/Version**: C# / .NET 9 (ASP.NET Core Web API)
**Primary Dependencies**:
- ASP.NET Core 9.x (Web API framework)
- HttpClient with Polly 8.x (HTTP client with retry/circuit breaker policies)
- Microsoft.Extensions.Http.Resilience (built-in resilience for HttpClient)
- Serilog 3.x (structured logging)
- FluentValidation 11.x (request validation)
- Hangfire 1.8.x (background job processing for retry queue)
- xUnit 2.x (unit testing)
- WireMock.Net (integration testing - MES API mocking)

**Storage**:
- SQLite (local queue for offline data persistence)
- Entity Framework Core 9.x (data access for queue management)
- Configuration: appsettings.json (MES endpoints, machine config, retry policies)

**Testing**:
- xUnit (unit tests, integration tests, contract tests)
- FluentAssertions (assertion library)
- WireMock.Net (mock MES API server for integration tests)
- Microsoft.AspNetCore.Mvc.Testing (Web API integration testing)

**Target Platform**: Windows Server 2019+ / Windows 10+ (primary deployment)
**Project Type**: Web API (ASP.NET Core backend service)
**Performance Goals**:
- API response time: <200ms p95 (excluding MES upstream latency)
- Support 10 concurrent equipment connections
- Handle 100 inspection uploads per minute per machine
- Token caching: <5ms cache hit latency

**Constraints**:
- MUST preserve IP address binding for authentication (Referrer header enforcement)
- MUST support offline operation with local queueing (no data loss during MES downtime)
- MUST implement exponential backoff retry (max 5 retries over 2 minutes)
- MUST NOT log authentication tokens or sensitive data
- MUST validate all upstream MES responses (success/code fields)

**Scale/Scope**:
- 5-20 production equipment instances per deployment
- ~1000 inspection records per day per machine
- 10-20 defects per inspection record (typical)
- Up to 99 defect categories per equipment type
- Support 5+ equipment types (AOI, AVI, VRS, ET, blind hole detection)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ Principle I: API-First Integration

**Requirement**: Strictly adhere to Cimforce Trace Management System API specification (CIMFORCE-WPT-20240902)

**Compliance**:
- ✅ Device login via `/api/prtmac/prtmacuserlogin` with token authentication
- ✅ Data upload via `/api/v1/MesTrace/TraceData/AddData3` with accessToken header
- ✅ Batch validation via `/api/transcode/checkcode` with accessToken header
- ✅ Exact request/response structures preserved (success, data, msg, code fields)
- ✅ IP address validation enforced (Referrer header binding)
- ✅ Machine number (PrtMacNo) binding implemented
- ✅ All documented error codes handled (200=success, 300=IP mismatch, -1=business errors)

**Status**: PASS - Full API contract compliance

### ✅ Principle II: Token-Based Security

**Requirement**: Token-based authentication for all MES operations

**Compliance**:
- ✅ Device login obtains long-lived token via `/api/prtmac/prtmacuserlogin`
- ✅ All subsequent requests include `accessToken` header
- ✅ Token refresh on IP/device name change
- ✅ IP address binding enforced via Referrer header
- ✅ Tokens stored securely (in-memory cache, NOT in logs)
- ✅ Token sanitization in error messages and logs

**Status**: PASS - Security requirements fully met

### ✅ Principle III: Test-First Development (TDD) - Red-Green-Refactor Cycle

**Requirement**: Mandatory Red-Green-Refactor cycle for ALL code

**Compliance**:
- ✅ Contract tests FIRST: Validate exact API request/response formats (paramData, benchmarks, otherData)
- ✅ Integration tests: Verify end-to-end data flow with mock MES (WireMock.Net)
- ✅ Unit tests: Cover data transformations, validation logic, error handling
- ✅ Tests written BEFORE implementation (Red phase)
- ✅ Minimum code to pass tests (Green phase)
- ✅ Refactor with passing tests as safety net (Refactor phase)

**Testing Strategy**:
1. 🔴 RED: Write contract test for device login → Test fails (endpoint not implemented)
2. 🟢 GREEN: Implement device login → Test passes
3. 🔵 REFACTOR: Extract token management service → Tests still pass
4. Repeat for data upload, batch validation, retry logic

**Status**: PASS - TDD workflow enforced throughout implementation

### ✅ Principle IV: Structured Data Contracts

**Requirement**: Follow three-tier data model (paramData/benchmarks/otherData)

**Compliance**:
- ✅ **paramData**: Summary data (Result, CheckQty, DefectQty, OkQty) with standardized codes
- ✅ **benchmarks**: Detailed data (Defect_Qty_01-99, Defect_Pos_01-99, Check_Param_01-99) up to 99 items per category
- ✅ **otherData**: Auxiliary info (LayersType, SourceType, CheckTime)
- ✅ Mandatory fields included: rowNo, procName, devName, userName, workClass
- ✅ Either traceCode OR lotNo required (at least one)
- ✅ ISO-formatted timestamps for CheckTime
- ✅ Units specified for all measured values
- ✅ Recommended parameter codes followed

**Status**: PASS - Data contract compliance enforced via FluentValidation

### ✅ Principle V: Error Handling & Observability

**Requirement**: Robust error handling and comprehensive logging

**Compliance**:
- ✅ All API responses validated for success/failure (success field, code field)
- ✅ Error messages captured with context (machine number, operation, timestamp)
- ✅ Mixed-batch validation errors reported clearly to operators
- ✅ Structured logging with Serilog (operation type, device info, trace codes, results)
- ✅ Performance metrics tracked: API response times, success rates, batch sizes
- ✅ Audit trail enables debugging without production reproduction

**Status**: PASS - Comprehensive observability implemented

### ✅ Principle VI: Cross-Platform Compatibility

**Requirement**: Windows deployment with path to .NET Core/Linux expansion

**Compliance**:
- ✅ Windows-first implementation (primary deployment target: Windows Server 2019+)
- ✅ .NET 9 technology stack (cross-platform capable)
- ✅ Platform-agnostic path handling (Path.Combine, no hardcoded backslashes)
- ✅ Standard .NET configuration providers (appsettings.json, environment variables)
- ✅ No Windows-specific P/Invoke or COM dependencies
- ✅ HTTPClient + Polly (cross-platform libraries)

**Status**: PASS - Future Linux migration path preserved

### Gate Summary

| Principle | Status | Notes |
|-----------|--------|-------|
| I. API-First Integration | ✅ PASS | Full API contract compliance |
| II. Token-Based Security | ✅ PASS | IP-bound tokens, secure storage |
| III. Test-First Development | ✅ PASS | TDD Red-Green-Refactor enforced |
| IV. Structured Data Contracts | ✅ PASS | Three-tier model validated |
| V. Error Handling & Observability | ✅ PASS | Serilog + performance metrics |
| VI. Cross-Platform Compatibility | ✅ PASS | Windows-first, Linux-ready |

**Overall Gate Status**: ✅ ALL GATES PASS - Proceed to Phase 0 Research

## Project Structure

### Documentation (this feature)

```text
specs/1-mes-trace-integration/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── device-login.http      # Device login API contract
│   ├── data-upload.http       # Inspection data upload contract
│   └── batch-validation.http  # Trace code validation contract
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── MesTraceApi/                    # ASP.NET Core Web API project
│   ├── Controllers/
│   │   ├── DeviceAuthController.cs       # Device login endpoint
│   │   ├── InspectionDataController.cs   # Data upload endpoint
│   │   └── ValidationController.cs       # Batch validation endpoint
│   ├── Models/
│   │   ├── Requests/
│   │   │   ├── DeviceLoginRequest.cs
│   │   │   ├── InspectionDataRequest.cs
│   │   │   └── BatchValidationRequest.cs
│   │   ├── Responses/
│   │   │   ├── MesApiResponse.cs         # Standard success/msg/data/code wrapper
│   │   │   ├── DeviceLoginResponse.cs
│   │   │   └── BatchValidationResponse.cs
│   │   └── Entities/
│   │       ├── InspectionRecord.cs
│   │       ├── ParamDataItem.cs
│   │       ├── BenchmarkItem.cs
│   │       └── OtherDataItem.cs
│   ├── Services/
│   │   ├── IMesClientService.cs
│   │   ├── MesClientService.cs           # HTTP client for MES API
│   │   ├── ITokenService.cs
│   │   ├── TokenService.cs               # Token cache and refresh logic
│   │   ├── IQueueService.cs
│   │   └── QueueService.cs               # Offline data queue with retry
│   ├── Validation/
│   │   ├── DeviceLoginValidator.cs
│   │   ├── InspectionDataValidator.cs
│   │   └── BatchValidationValidator.cs
│   ├── Infrastructure/
│   │   ├── Data/
│   │   │   ├── QueueDbContext.cs         # EF Core context for SQLite
│   │   │   └── Migrations/
│   │   ├── Logging/
│   │   │   └── LoggingExtensions.cs      # Serilog enrichers
│   │   └── Resilience/
│   │       └── ResiliencePolicies.cs     # Polly retry/circuit breaker
│   ├── BackgroundJobs/
│   │   └── RetryQueueJob.cs              # Hangfire job for retry
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Program.cs
│   └── MesTraceApi.csproj
│
└── MesTraceApi.Client/                    # Optional: Client SDK for equipment integration
    ├── IMesTraceClient.cs
    ├── MesTraceClient.cs
    └── MesTraceApi.Client.csproj

tests/
├── MesTraceApi.Tests/
│   ├── Contract/
│   │   ├── DeviceLoginContractTests.cs   # Test exact API request/response formats
│   │   ├── DataUploadContractTests.cs
│   │   └── BatchValidationContractTests.cs
│   ├── Integration/
│   │   ├── DeviceAuthFlowTests.cs        # End-to-end auth flow with WireMock
│   │   ├── DataUploadFlowTests.cs        # End-to-end upload with retry
│   │   ├── OfflineQueueTests.cs          # Offline operation + recovery
│   │   └── TestWebApplicationFactory.cs  # Test server setup
│   └── Unit/
│       ├── Services/
│       │   ├── MesClientServiceTests.cs
│       │   ├── TokenServiceTests.cs
│       │   └── QueueServiceTests.cs
│       └── Validation/
│           ├── DeviceLoginValidatorTests.cs
│           ├── InspectionDataValidatorTests.cs
│           └── BatchValidationValidatorTests.cs
└── MesTraceApi.Tests.csproj
```

**Structure Decision**: Web API architecture with single backend project. No frontend required (this is a backend integration service consumed by production equipment). The MesTraceApi project hosts REST endpoints that mirror MES API contracts, handle authentication/token management, implement offline queueing, and provide observability. An optional client SDK (MesTraceApi.Client) can wrap the API for easier equipment integration. Tests organized by contract/integration/unit layers following TDD Red-Green-Refactor discipline.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

**No violations detected** - All constitution principles satisfied. No additional complexity justification required.
