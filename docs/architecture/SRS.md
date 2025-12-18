# Software Requirements Specification (SRS)
## MES Middleware Service - Web API Architecture

**Version**: 2.0 (Web API Migration)
**Date**: 2025-11-20
**Project**: MES Middleware Service
**Status**: Production-Ready (User Story 1 & 2 Complete)

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [System Overview](#2-system-overview)
3. [Architecture](#3-architecture)
4. [Functional Requirements](#4-functional-requirements)
5. [Non-Functional Requirements](#5-non-functional-requirements)
6. [User Stories](#6-user-stories)
7. [API Specification](#7-api-specification)
8. [Data Models](#8-data-models)
9. [Configuration](#9-configuration)
10. [Deployment](#10-deployment)
11. [Testing Strategy](#11-testing-strategy)
12. [Migration from Shared Memory Architecture](#12-migration-from-shared-memory-architecture)

---

## 1. Introduction

### 1.1 Purpose

This document specifies the requirements for the MES Middleware Service, a production-grade Windows Service that bridges manufacturing equipment inspection data with the MES Cloud system via HTTP REST API. The system provides real-time data collection, offline queue management, and a WPF monitoring dashboard.

### 1.2 Scope

**In Scope**:
- Equipment data collection via HTTP POST API (User Story 1)
- Real-time monitoring dashboard with WPF application (User Story 2)
- Offline upload queue with SQLite persistence
- Background retry processing with Hangfire
- Multi-language support (zh-TW, zh-CN, en)
- Windows Service deployment with embedded Kestrel web server

**Out of Scope**:
- Bidirectional command & control (User Story 3 - deferred to future release)
- LabVIEW direct integration (equipment side implementation)
- Cloud-side MES API implementation

### 1.3 Definitions and Acronyms

- **MES**: Manufacturing Execution System
- **IPC**: Inter-Process Communication
- **REST API**: Representational State Transfer Application Programming Interface
- **WPF**: Windows Presentation Foundation
- **SRS**: Software Requirements Specification
- **DTO**: Data Transfer Object
- **MVVM**: Model-View-ViewModel pattern
- **Kestrel**: ASP.NET Core web server

### 1.4 References

- ASP.NET Core Documentation: https://docs.microsoft.com/aspnet/core
- .NET 9 Documentation: https://docs.microsoft.com/dotnet
- Hangfire Documentation: https://docs.hangfire.io
- FluentValidation Documentation: https://docs.fluentvalidation.net

---

## 2. System Overview

### 2.1 System Context

```
┌─────────────────┐         HTTP POST          ┌──────────────────────────┐
│   Equipment     │─────────────────────────────▶│  MES Middleware Service  │
│  (LabVIEW/C#)   │    /api/inspection/submit   │  (Windows Service +      │
└─────────────────┘                             │   Kestrel Web Server)    │
                                                │                          │
                                                │  ┌───────────────────┐   │
                                                │  │ Channel Queue     │   │
                                                │  │ (In-Memory)       │   │
                                                │  └────────┬──────────┘   │
                                                │           │              │
                                                │  ┌────────▼──────────┐   │
                                                │  │ SQLite Queue      │   │
                                                │  │ (Persistent)      │   │
                                                │  └────────┬──────────┘   │
                                                │           │              │
                                                │  ┌────────▼──────────┐   │
                                                │  │ Hangfire Retry    │   │
                                                │  │ (Background Jobs) │   │
                                                │  └────────┬──────────┘   │
                                                └───────────┼──────────────┘
                                                            │ HTTPS
                                                            │
                                                ┌───────────▼──────────────┐
                                                │   MES Cloud Web API      │
                                                │ (External, JWT Auth)     │
                                                └──────────────────────────┘

┌─────────────────┐         HTTP GET           ┌──────────────────────────┐
│  WPF Monitor    │◀─────────────────────────────│  MES Middleware Service  │
│  (Desktop App)  │      /api/status            │  (StatusController)      │
└─────────────────┘      /api/status/history    └──────────────────────────┘
```

### 2.2 Key Components

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **MES Middleware Service** | ASP.NET Core Web API + Windows Service | Core backend service for data processing |
| **Kestrel Web Server** | Embedded in Service | HTTP endpoint hosting |
| **InspectionController** | ASP.NET Core Controller | Equipment data submission endpoint |
| **StatusController** | ASP.NET Core Controller | WPF monitoring endpoints |
| **InspectionChannelProcessor** | BackgroundService | Processes channel queue data |
| **UploadQueueService** | EF Core + SQLite | Persistent offline queue |
| **MesWebApiClient** | HttpClient + Polly | MES Cloud API communication |
| **Hangfire** | Background Job Scheduler | Retry failed uploads with exponential backoff |
| **WPF Monitor** | WPF + MVVM | Real-time monitoring dashboard |

### 2.3 Technology Stack

- **Platform**: .NET 9
- **Backend**: ASP.NET Core Web API
- **Frontend**: WPF (Windows Presentation Foundation)
- **Database**: Entity Framework Core 9 + SQLite
- **Job Scheduling**: Hangfire 1.8.x
- **Validation**: FluentValidation 11.x
- **Logging**: Serilog 3.x
- **Resilience**: Polly (circuit breaker, retry policies)
- **MVVM Framework**: CommunityToolkit.Mvvm
- **API Documentation**: Swagger/OpenAPI (Swashbuckle.AspNetCore 10.x)

---

## 3. Architecture

### 3.1 Three-Tier Queue Architecture

The system implements a three-tier queue for reliability and performance:

#### Tier 1: In-Memory Channel Queue
- **Implementation**: `System.Threading.Channels.Channel<InspectionRecord>`
- **Capacity**: 1000 items (configurable)
- **Behavior**: `BoundedChannelFullMode.Wait` (backpressure handling)
- **Purpose**: High-speed buffering for incoming HTTP requests
- **Performance**: Sub-millisecond write latency

#### Tier 2: SQLite Persistent Queue
- **Implementation**: Entity Framework Core + SQLite
- **Table**: `UploadQueue`
- **Purpose**: Offline storage when MES Cloud API is unavailable
- **Features**: CRUD operations, retry tracking, failure logging

#### Tier 3: Hangfire Retry Jobs
- **Implementation**: Hangfire background job scheduler
- **Retry Policy**: Exponential backoff (2s, 4s, 8s, 16s, 32s)
- **Max Retries**: 5 attempts (configurable)
- **Dashboard**: Available at `http://localhost:5100/hangfire`

### 3.2 Data Flow

```
1. Equipment → HTTP POST /api/inspection/submit
   ↓
2. InspectionController validates JSON (FluentValidation)
   ↓
3. Write to Channel<InspectionRecord> (Tier 1)
   ↓ (async background processing)
4. InspectionChannelProcessor reads from channel
   ↓
5. Attempt upload to MES Cloud API (MesWebApiClient)
   ↓
6. Success? → Log to database (TotalReceived++)
   ↓
7. Failure? → Save to SQLite Queue (Tier 2)
   ↓
8. Schedule Hangfire retry job (Tier 3)
   ↓
9. Exponential backoff retries
   ↓
10. WPF Monitor polls GET /api/status for statistics
```

### 3.3 Service Architecture

#### Windows Service Hosting

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(); // Enable Windows Service support

// ... service registration ...

var app = builder.Build();
app.Run("http://localhost:5100"); // Kestrel listening
```

#### Dependency Injection Container

```csharp
// Core Services
builder.Services.AddDbContext<MiddlewareDbContext>(options =>
    options.UseSqlite("Data Source=Data/middleware.db"));

// Channel Queue (Tier 1)
var channelOptions = new BoundedChannelOptions(1000)
{
    FullMode = BoundedChannelFullMode.Wait
};
var channel = Channel.CreateBounded<InspectionRecord>(channelOptions);
builder.Services.AddSingleton(channel);

// Background Services
builder.Services.AddHostedService<InspectionChannelProcessor>();

// HTTP Clients
builder.Services.AddHttpClient<IMesWebApiClient, MesWebApiClient>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

// Validators
builder.Services.AddValidatorsFromAssemblyContaining<InspectionRecordValidator>();

// Hangfire
builder.Services.AddHangfire(config => config.UseSQLiteStorage("Data/hangfire.db"));
builder.Services.AddHangfireServer();
```

---

## 4. Functional Requirements

### 4.1 Equipment Data Collection (User Story 1)

#### FR-001: HTTP Data Submission Endpoint
**Priority**: Critical
**Description**: The system SHALL provide an HTTP POST endpoint `/api/inspection/submit` to receive inspection data from equipment.

**Acceptance Criteria**:
- Endpoint accepts JSON body conforming to `InspectionRecord` schema
- Returns HTTP 202 Accepted for valid submissions
- Returns HTTP 400 Bad Request with validation error details for invalid data
- Response time < 100ms (p95)

#### FR-002: JSON Schema Validation
**Priority**: Critical
**Description**: The system SHALL validate incoming JSON against the defined schema using FluentValidation.

**Validation Rules**:
- `MachineNumber`: Required, max length 50
- `SerialNumber`: Required, max length 100
- `InspectionResult`: Required, enum values ("OK", "NG", "Recheck")
- `InspectionTime`: Required, valid DateTime
- `MeasurementData`: Optional, valid JSON object

**Acceptance Criteria**:
- Invalid data returns HTTP 400 with specific error messages
- Validation errors are logged
- Valid data passes through to channel queue

#### FR-003: In-Memory Channel Queue
**Priority**: Critical
**Description**: The system SHALL buffer incoming data in a bounded in-memory channel queue.

**Acceptance Criteria**:
- Channel capacity: 1000 items (configurable via `appsettings.json`)
- Full mode: Wait (blocks HTTP response until space available)
- Write latency: < 1ms (p99)
- Thread-safe concurrent writes

#### FR-004: Background Processing
**Priority**: Critical
**Description**: The system SHALL process queued data asynchronously via a BackgroundService.

**Acceptance Criteria**:
- `InspectionChannelProcessor` reads from channel in continuous loop
- Processing continues even during HTTP API failures
- Graceful shutdown: waits for current item to complete (max 30s timeout)

#### FR-005: MES Cloud API Upload
**Priority**: Critical
**Description**: The system SHALL upload validated data to the MES Cloud API via HTTPS.

**Acceptance Criteria**:
- Endpoint: Configured via `appsettings.json` (`WebApi:BaseUrl`)
- Authentication: JWT token with automatic refresh
- Timeout: 30 seconds (configurable)
- Request body: JSON serialization of `InspectionRecord`
- Success response: HTTP 200-299 status codes

#### FR-006: Offline Queue Persistence
**Priority**: Critical
**Description**: The system SHALL persist failed uploads to SQLite database for retry.

**Acceptance Criteria**:
- Failed uploads are saved to `UploadQueue` table
- Record includes: Guid ID, JSON payload, retry count, failure reason, timestamp
- Database path: `Data/queue.db` (configurable)
- Maximum queue size: Unlimited (monitor disk space)

#### FR-007: Exponential Backoff Retry
**Priority**: High
**Description**: The system SHALL retry failed uploads using Hangfire with exponential backoff.

**Retry Schedule**:
1. Immediate failure → Save to SQLite
2. After 2 seconds → 1st retry
3. After 4 seconds → 2nd retry
4. After 8 seconds → 3rd retry
5. After 16 seconds → 4th retry
6. After 32 seconds → 5th retry (final)

**Acceptance Criteria**:
- Max retries: 5 (configurable via `Queue:MaxRetries`)
- After 5 failures, mark as "permanently failed"
- Failed records remain in database for manual retry

#### FR-008: Success Statistics Tracking
**Priority**: Medium
**Description**: The system SHALL track upload statistics for monitoring purposes.

**Metrics**:
- `TotalReceived`: Total HTTP POST requests received
- `SuccessfulUploads`: Uploads that succeeded (including retries)
- `QueuedUploads`: Currently queued for retry
- `QueueDepth`: Current channel queue depth

**Acceptance Criteria**:
- Statistics are updated in real-time
- Available via GET `/api/status` endpoint
- Thread-safe counter increments

---

### 4.2 Real-Time Monitoring Dashboard (User Story 2)

#### FR-009: WPF Status Dashboard
**Priority**: High
**Description**: The system SHALL provide a WPF desktop application for monitoring service status.

**UI Components**:
- Connection status indicator (Green/Yellow/Red)
- Real-time statistics display (Total/Success/Queued)
- Last ping timestamp
- Error message display
- Manual refresh button

**Acceptance Criteria**:
- MVVM architecture with data binding
- Auto-refresh every 2 seconds (configurable)
- Supports multi-language (zh-TW, zh-CN, en)

#### FR-010: Status Polling Endpoint
**Priority**: High
**Description**: The system SHALL provide GET `/api/status` endpoint for WPF polling.

**Response Schema**:
```json
{
  "status": "Connected",
  "lastPingTimestamp": "2025-11-20T10:30:45Z",
  "queueDepth": 0,
  "totalReceived": 1234,
  "successfulUploads": 1230,
  "queuedUploads": 4
}
```

**Status Values**:
- `Connected`: MES Cloud API reachable, queue empty
- `Retrying`: Temporary failures, retrying uploads
- `Disconnected`: MES Cloud API unreachable, queue growing
- `Error`: Service malfunction

**Acceptance Criteria**:
- Response time: < 50ms
- Returns 200 OK even if backend API is down
- Thread-safe access to shared statistics

#### FR-011: Upload History Display
**Priority**: Medium
**Description**: The WPF application SHALL display recent upload history.

**Features**:
- Show last 100 uploads (configurable)
- Columns: Timestamp, Machine Number, Serial Number, Result, Status, Retry Count
- Filter by status: All/Success/Failed/Retrying
- Auto-refresh with status updates

**Acceptance Criteria**:
- Data fetched from GET `/api/status/history?limit=100`
- Displays in DataGrid with sorting
- Updates every 2 seconds

#### FR-012: Manual Retry Trigger
**Priority**: Low
**Description**: Users SHALL be able to manually retry failed uploads from WPF UI.

**Acceptance Criteria**:
- Right-click context menu on failed item → "Retry Upload"
- Calls POST `/api/status/queue/{id}/retry`
- Shows success/failure notification
- Updates history grid on completion

#### FR-013: System Tray Integration
**Priority**: Low
**Description**: The WPF application SHALL minimize to system tray.

**Features**:
- Tray icon changes color based on status (Green/Yellow/Red)
- Balloon notifications for errors
- Right-click menu: Open Dashboard, Exit

**Acceptance Criteria**:
- Window hides (not closes) when minimized
- Double-click tray icon restores window
- Notifications configurable via settings

---

### 4.3 Configuration Management

#### FR-014: JSON Configuration File
**Priority**: High
**Description**: The system SHALL support runtime configuration via `appsettings.json`.

**Configuration Sections**:
```json
{
  "Urls": "http://localhost:5100",
  "WebApi": {
    "BaseUrl": "https://mes-cloud.company.com",
    "Username": "middleware_user",
    "Password": "encrypted_password",
    "Timeout": 30,
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
  },
  "Resilience": {
    "Retry": {
      "MaxRetryAttempts": 5,
      "InitialDelay": 2,
      "BackoffType": "Exponential"
    },
    "CircuitBreaker": {
      "FailureRatio": 0.5,
      "SamplingDuration": 30,
      "MinimumThroughput": 10
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/middleware-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

**Acceptance Criteria**:
- Environment-specific overrides (appsettings.Production.json)
- Sensitive data encrypted (passwords)
- Configuration validation at startup

#### FR-015: Multi-Language Support
**Priority**: Medium
**Description**: The WPF application SHALL support runtime language switching.

**Supported Languages**:
- `zh-TW`: 繁體中文 (Traditional Chinese) - Default
- `zh-CN`: 简体中文 (Simplified Chinese)
- `en`: English

**Acceptance Criteria**:
- Language stored in user settings
- UI updates without restart
- Resource files: `Resources/Strings.zh-TW.resx`, `Strings.zh-CN.resx`, `Strings.en.resx`

---

### 4.4 Logging and Monitoring

#### FR-016: Structured Logging
**Priority**: High
**Description**: The system SHALL log all operations using Serilog structured logging.

**Log Levels**:
- **Debug**: Channel queue operations, HTTP request/response details
- **Information**: Successful uploads, service start/stop, configuration loaded
- **Warning**: Retry attempts, circuit breaker open, queue approaching capacity
- **Error**: Upload failures, validation errors, database errors
- **Critical**: Service crashes, unhandled exceptions

**Log Outputs**:
- Console (development)
- File: `logs/middleware-{Date}.log` (daily rolling, 7-day retention)

**Acceptance Criteria**:
- Logs include correlation IDs for request tracing
- JSON-formatted structured logs
- Performance: Logging does not exceed 5% CPU overhead

#### FR-017: Health Check Endpoint
**Priority**: Medium
**Description**: The system SHALL provide a health check endpoint for monitoring tools.

**Endpoint**: GET `/health`

**Response**:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "mesCloudApi": "Healthy",
    "channelQueue": "Healthy"
  },
  "uptime": "12:34:56"
}
```

**Health States**:
- `Healthy`: All systems operational
- `Degraded`: Offline queue active, but functioning
- `Unhealthy`: Critical component failure

**Acceptance Criteria**:
- Returns 200 OK when healthy
- Returns 503 Service Unavailable when unhealthy
- Response time < 100ms

#### FR-018: Hangfire Dashboard
**Priority**: Low
**Description**: The system SHALL provide Hangfire web dashboard for job monitoring.

**URL**: `http://localhost:5100/hangfire`

**Features**:
- View queued/processing/succeeded/failed jobs
- Retry failed jobs manually
- View job execution history
- Real-time job statistics

**Acceptance Criteria**:
- Dashboard accessible from localhost only (security)
- No authentication required (localhost trust)
- Read-only for production environments

---

## 5. Non-Functional Requirements

### 5.1 Performance

#### NFR-001: Throughput
- **HTTP Endpoint**: 100+ requests/second
- **Channel Queue**: 1000+ writes/second
- **Database Write**: 50+ transactions/second

#### NFR-002: Latency
- **HTTP Response Time**: < 100ms (p95), < 200ms (p99)
- **Channel Write**: < 1ms (p99)
- **Status Endpoint**: < 50ms (p95)

#### NFR-003: Memory Usage
- **Service Process**: < 200MB RAM (idle)
- **WPF Application**: < 100MB RAM
- **Channel Queue**: < 50MB (at capacity)

#### NFR-004: Disk I/O
- **SQLite Write**: < 10ms per transaction
- **Log Write**: Async, non-blocking

### 5.2 Reliability

#### NFR-005: Availability
- **Target**: 99.5% uptime (excluding planned maintenance)
- **Downtime Tolerance**: < 4 hours/month
- **Recovery Time Objective (RTO)**: < 5 minutes

#### NFR-006: Data Durability
- **Offline Queue**: 100% persistence (SQLite ACID compliance)
- **Channel Queue**: Volatile (data lost on crash)
- **Backup**: Daily automated backup of SQLite database

#### NFR-007: Error Recovery
- **Transient Failures**: Automatic retry with exponential backoff
- **Permanent Failures**: Manual intervention required (logged)
- **Crash Recovery**: Service auto-restarts (Windows Service recovery policy)

### 5.3 Security

#### NFR-008: Authentication
- **MES Cloud API**: JWT bearer token authentication
- **Token Refresh**: Automatic renewal before expiration
- **Credentials Storage**: Encrypted in `appsettings.json` (Windows DPAPI recommended)

#### NFR-009: Network Security
- **Transport**: HTTPS/TLS 1.2+ for MES Cloud API
- **Local Endpoint**: HTTP (localhost only, no external exposure)
- **Firewall**: Restrict port 5100 to localhost

#### NFR-010: Input Validation
- **HTTP Requests**: FluentValidation on all inputs
- **SQL Injection**: EF Core parameterized queries (no raw SQL)
- **JSON Deserialization**: Strict schema validation

### 5.4 Maintainability

#### NFR-011: Code Quality
- **Test Coverage**: > 80% (unit + integration tests)
- **Static Analysis**: Zero critical/high severity warnings
- **Code Style**: Follow .NET coding conventions

#### NFR-012: Documentation
- **API Documentation**: Swagger/OpenAPI interactive docs at `/swagger`
- **Code Comments**: XML documentation comments on public APIs
- **README**: Quick start guide and troubleshooting

#### NFR-013: Versioning
- **Semantic Versioning**: Major.Minor.Patch (e.g., 2.0.0)
- **Database Migrations**: EF Core versioned migrations
- **API Versioning**: URL-based versioning (/api/v1/...)

### 5.5 Scalability

#### NFR-014: Horizontal Scalability
- **Not Supported**: Single-instance design (Windows Service per machine)
- **Reason**: Localhost-only endpoint, machine-specific data

#### NFR-015: Vertical Scalability
- **CPU**: Scales to 4+ cores (async/await parallelism)
- **Memory**: Bounded channel prevents unbounded growth
- **Disk**: SQLite supports up to 140TB database size

### 5.6 Compatibility

#### NFR-016: Platform Compatibility
- **Operating System**: Windows 10/11, Windows Server 2019/2022
- **.NET Runtime**: .NET 9.0+
- **Database**: SQLite 3.x (bundled)

#### NFR-017: Equipment Integration
- **Supported Clients**: Any HTTP/1.1 client (LabVIEW, C#, Python, cURL)
- **Data Format**: JSON (UTF-8 encoding)
- **Network**: IPv4 localhost (127.0.0.1)

---

## 6. User Stories

### User Story 1: Equipment Data Collection via HTTP API

**As a** manufacturing equipment controller (LabVIEW/C# software)
**I want to** submit inspection data via HTTP POST API
**So that** my data is reliably uploaded to MES Cloud even during network outages

#### Acceptance Criteria

1. **Given** equipment completes an inspection
   **When** it sends HTTP POST to `http://localhost:5100/api/inspection/submit`
   **Then** it receives HTTP 202 Accepted response within 100ms

2. **Given** MES Cloud API is temporarily unavailable
   **When** equipment submits data
   **Then** data is queued to SQLite and retried automatically

3. **Given** equipment sends invalid JSON (missing required fields)
   **When** middleware validates the request
   **Then** it returns HTTP 400 with specific validation errors

4. **Given** 1000 items are queued in the channel
   **When** equipment submits another item
   **Then** HTTP response waits until queue has space (backpressure)

#### Tasks (Completed: 39/39)

- ✅ T-M001: Create InspectionController with POST endpoint
- ✅ T-M002: Implement FluentValidation for InspectionRecord
- ✅ T-M003: Configure Channel<InspectionRecord> with bounded capacity
- ✅ T-M004: Create InspectionChannelProcessor BackgroundService
- ✅ T-M005: Update Program.cs from Host to WebApplication
- ✅ T-M006: Add Swagger/OpenAPI documentation
- ✅ T-M007: Implement MesWebApiClient with Polly retry
- ✅ T-M008: Update UploadQueueService for SQLite persistence
- ✅ T-M009: Configure Hangfire background jobs
- ✅ T-M010: Write integration tests for HTTP endpoint
- ✅ T-M011: Write integration tests for offline queue
- ✅ T001-T027: Original shared memory implementation (archived)

---

### User Story 2: Real-Time Monitoring Dashboard

**As a** factory floor operator
**I want to** monitor the middleware service status in real-time via WPF application
**So that** I can quickly identify and resolve upload issues

#### Acceptance Criteria

1. **Given** the middleware service is running
   **When** I open the WPF Monitor application
   **Then** I see connection status (Green/Yellow/Red) and real-time statistics

2. **Given** the service is processing uploads
   **When** I view the History tab
   **Then** I see the last 100 uploads with timestamps, results, and retry counts

3. **Given** an upload has failed 5 times
   **When** I right-click the item and select "Retry Upload"
   **Then** the system immediately retries and updates the status

4. **Given** I minimize the WPF application
   **When** an error occurs
   **Then** I see a balloon notification in the system tray

5. **Given** I prefer Simplified Chinese
   **When** I change the language setting
   **Then** the entire UI updates to zh-CN without restart

#### Tasks (Completed: 26/26)

- ✅ T-M012: Create StatusController with GET /api/status endpoint
- ✅ T-M013: Create GET /api/status/history endpoint
- ✅ T-M014: Create POST /api/status/queue/{id}/retry endpoint
- ✅ T-M015: Update MiddlewareApiClient to use HTTP polling
- ✅ T-M016: Update StatusViewModel to poll every 2 seconds
- ✅ T-M017: Update HistoryViewModel for HTTP data source
- ✅ T-M018: Update App.xaml.cs DI registration
- ✅ T-M019: Update ConnectionStatusDto with new properties
- ✅ T-M020: Write unit tests for StatusViewModel
- ✅ T-M021: Write unit tests for HistoryViewModel
- ✅ T028-T041: Original WPF implementation

---

### User Story 3: Bidirectional Command & Control (NOT IMPLEMENTED)

**Status**: ❌ Deferred to Future Release
**Progress**: 0% (0/14 tasks completed)

**As a** MES system administrator
**I want to** send commands to equipment via the middleware
**So that** I can remotely control inspection parameters or trigger calibration

**Rationale for Deferral**:
- HTTP polling is inefficient for real-time bidirectional communication
- Preferred approach: WebSocket or Server-Sent Events (SSE)
- Requires equipment-side implementation (LabVIEW/C# client updates)
- Low priority compared to data collection (US1) and monitoring (US2)

**Future Implementation Options**:
1. **WebSocket**: Full-duplex real-time communication
2. **Server-Sent Events (SSE)**: Server-to-client push notifications
3. **Long Polling**: HTTP-based simulation of push

**Estimated Tasks**: 14 tasks (T056-T069) + 15-20 additional tests

---

## 7. API Specification

### 7.1 Equipment Data Submission

#### POST /api/inspection/submit

Submit inspection data from equipment to middleware.

**Request**:
```http
POST /api/inspection/submit HTTP/1.1
Host: localhost:5100
Content-Type: application/json

{
  "machineNumber": "MACHINE-01",
  "serialNumber": "SN20251120001",
  "inspectionResult": "OK",
  "inspectionTime": "2025-11-20T10:30:45Z",
  "measurementData": {
    "diameter": 25.4,
    "length": 100.0,
    "defects": []
  }
}
```

**Response (Success)**:
```http
HTTP/1.1 202 Accepted
Content-Type: application/json

{
  "message": "Inspection data accepted for processing",
  "timestamp": "2025-11-20T10:30:45Z"
}
```

**Response (Validation Error)**:
```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "errors": [
    {
      "propertyName": "MachineNumber",
      "errorMessage": "'Machine Number' must not be empty.",
      "attemptedValue": null
    }
  ]
}
```

**Schema Validation**:
- `machineNumber`: string, required, max 50 chars
- `serialNumber`: string, required, max 100 chars
- `inspectionResult`: enum ("OK", "NG", "Recheck"), required
- `inspectionTime`: ISO 8601 datetime, required
- `measurementData`: object, optional (free-form JSON)

---

### 7.2 Service Status Monitoring

#### GET /api/status

Retrieve current service status and statistics.

**Request**:
```http
GET /api/status HTTP/1.1
Host: localhost:5100
```

**Response**:
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "Connected",
  "lastPingTimestamp": "2025-11-20T10:35:12Z",
  "queueDepth": 0,
  "totalReceived": 1234,
  "successfulUploads": 1230,
  "queuedUploads": 4
}
```

**Status Values**:
- `Connected`: MES Cloud API healthy, no queued uploads
- `Retrying`: Temporary failures, retry in progress
- `Disconnected`: MES Cloud API unreachable, queue building
- `Error`: Service malfunction (check logs)

**Statistics**:
- `totalReceived`: Total POST requests received since startup
- `successfulUploads`: Uploads confirmed by MES Cloud (including retries)
- `queuedUploads`: Items currently in SQLite offline queue
- `queueDepth`: Items in in-memory channel queue

---

### 7.3 Upload History

#### GET /api/status/history

Retrieve recent upload history for monitoring dashboard.

**Request**:
```http
GET /api/status/history?limit=100&status=Failed HTTP/1.1
Host: localhost:5100
```

**Query Parameters**:
- `limit`: int, optional, default 100 (max 1000)
- `status`: string, optional, filter by status ("Pending", "Success", "Failed", "Retrying")

**Response**:
```http
HTTP/1.1 200 OK
Content-Type: application/json

[
  {
    "id": 42,
    "machineNumber": "MACHINE-01",
    "serialNumber": "SN20251120001",
    "result": "OK",
    "status": "Success",
    "retryCount": 0,
    "timestamp": "2025-11-20T10:30:45Z",
    "errorMessage": null
  },
  {
    "id": 41,
    "machineNumber": "MACHINE-01",
    "serialNumber": "SN20251120002",
    "result": "NG",
    "status": "Failed",
    "retryCount": 5,
    "timestamp": "2025-11-20T10:25:30Z",
    "errorMessage": "Timeout: MES Cloud API did not respond"
  }
]
```

---

### 7.4 Manual Retry

#### POST /api/status/queue/{id}/retry

Manually trigger retry for a failed upload.

**Request**:
```http
POST /api/status/queue/41/retry HTTP/1.1
Host: localhost:5100
```

**Response (Success)**:
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "message": "Retry job scheduled successfully",
  "jobId": "abc123def456"
}
```

**Response (Not Found)**:
```http
HTTP/1.1 404 Not Found
Content-Type: application/json

{
  "error": "Queue item 41 not found"
}
```

**Response (Already Succeeded)**:
```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "error": "Item already succeeded, retry not allowed"
}
```

---

### 7.5 Health Check

#### GET /health

Health check endpoint for monitoring tools.

**Request**:
```http
GET /health HTTP/1.1
Host: localhost:5100
```

**Response (Healthy)**:
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "mesCloudApi": "Healthy",
    "channelQueue": "Healthy"
  },
  "uptime": "12:34:56",
  "version": "2.0.0"
}
```

**Response (Unhealthy)**:
```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/json

{
  "status": "Unhealthy",
  "checks": {
    "database": "Healthy",
    "mesCloudApi": "Unhealthy",
    "channelQueue": "Healthy"
  },
  "uptime": "12:34:56",
  "version": "2.0.0",
  "errors": [
    "MES Cloud API unreachable: Connection timeout after 30s"
  ]
}
```

---

## 8. Data Models

### 8.1 InspectionRecord (DTO)

Equipment inspection data submitted via HTTP POST.

```csharp
public class InspectionRecord
{
    /// <summary>
    /// Machine identifier (e.g., "MACHINE-01")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MachineNumber { get; set; } = string.Empty;

    /// <summary>
    /// Product serial number (e.g., "SN20251120001")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Inspection result: "OK", "NG", or "Recheck"
    /// </summary>
    [Required]
    public string InspectionResult { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when inspection was performed
    /// </summary>
    [Required]
    public DateTime InspectionTime { get; set; }

    /// <summary>
    /// Free-form JSON object with measurement data
    /// </summary>
    public JsonElement? MeasurementData { get; set; }
}
```

**Validation Rules** (FluentValidation):
```csharp
public class InspectionRecordValidator : AbstractValidator<InspectionRecord>
{
    public InspectionRecordValidator()
    {
        RuleFor(x => x.MachineNumber)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.SerialNumber)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.InspectionResult)
            .NotEmpty()
            .Must(result => new[] { "OK", "NG", "Recheck" }.Contains(result))
            .WithMessage("InspectionResult must be 'OK', 'NG', or 'Recheck'");

        RuleFor(x => x.InspectionTime)
            .NotEmpty()
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
            .WithMessage("InspectionTime cannot be in the future");
    }
}
```

---

### 8.2 UploadQueueItem (Entity)

SQLite database entity for offline queue persistence.

```csharp
public class UploadQueueItem
{
    /// <summary>
    /// Primary key (GUID)
    /// </summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// JSON payload (serialized InspectionRecord)
    /// </summary>
    [Required]
    public string JsonPayload { get; set; } = string.Empty;

    /// <summary>
    /// Queue status: "Pending", "Retrying", "Success", "Failed"
    /// </summary>
    [Required]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Timestamp when item was queued
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of last retry attempt
    /// </summary>
    public DateTime? LastRetryAt { get; set; }

    /// <summary>
    /// Error message from last failed upload attempt
    /// </summary>
    public string? FailureReason { get; set; }
}
```

**Database Schema** (SQLite):
```sql
CREATE TABLE UploadQueue (
    Id TEXT PRIMARY KEY,
    JsonPayload TEXT NOT NULL,
    Status TEXT NOT NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    LastRetryAt TEXT,
    FailureReason TEXT
);

CREATE INDEX IX_Status ON UploadQueue(Status);
CREATE INDEX IX_CreatedAt ON UploadQueue(CreatedAt);
```

---

### 8.3 ConnectionStatusDto (DTO)

WPF monitoring dashboard status response.

```csharp
public class ConnectionStatusDto
{
    /// <summary>
    /// Connection status: "Connected", "Retrying", "Disconnected", "Error"
    /// </summary>
    public string Status { get; set; } = "Disconnected";

    /// <summary>
    /// Timestamp of last successful ping to MES Cloud API
    /// </summary>
    public DateTime LastPingTimestamp { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Current channel queue depth (0-1000)
    /// </summary>
    public int QueueDepth { get; set; }

    /// <summary>
    /// Total HTTP POST requests received since startup
    /// </summary>
    public int TotalReceived { get; set; }

    /// <summary>
    /// Total successful uploads (including retries)
    /// </summary>
    public int SuccessfulUploads { get; set; }

    /// <summary>
    /// Items currently queued in SQLite
    /// </summary>
    public int QueuedUploads { get; set; }
}
```

---

### 8.4 UploadHistoryDto (DTO)

Upload history item for WPF History tab.

```csharp
public class UploadHistoryDto
{
    public int Id { get; set; }
    public string MachineNumber { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
}
```

---

## 9. Configuration

### 9.1 appsettings.json (Development)

```json
{
  "Urls": "http://localhost:5100",
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "WebApi": {
    "BaseUrl": "https://dev-mes-api.company.com",
    "Timeout": 30,
    "MachineNumber": "DEV-MACHINE-01",
    "MachineIp": "192.168.1.100",
    "Username": "dev_middleware",
    "Password": "dev_password"
  },
  "InspectionChannel": {
    "Capacity": 1000,
    "FullMode": "Wait"
  },
  "Queue": {
    "DatabasePath": "Data/queue.db",
    "MaxRetries": 5,
    "RetryDelaySeconds": 2
  },
  "Resilience": {
    "Retry": {
      "MaxRetryAttempts": 5,
      "InitialDelay": 2,
      "BackoffType": "Exponential"
    },
    "CircuitBreaker": {
      "FailureRatio": 0.5,
      "SamplingDuration": 30,
      "MinimumThroughput": 10,
      "BreakDuration": 60
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Information",
        "System": "Warning",
        "Hangfire": "Information"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/middleware-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

---

### 9.2 appsettings.Production.json (Production Overrides)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning",
        "Hangfire": "Information"
      }
    }
  },
  "InspectionChannel": {
    "Capacity": 1000,
    "FullMode": "Wait"
  },
  "WebApi": {
    "BaseUrl": "https://production-mes-api.company.com",
    "Timeout": 30,
    "MachineNumber": "MACHINE001",
    "MachineIp": "192.168.1.100",
    "Username": "middleware",
    "Password": "*** REPLACE WITH ACTUAL PASSWORD ***"
  },
  "Queue": {
    "DatabasePath": "Data/queue.db",
    "MaxRetries": 5,
    "RetryDelaySeconds": 2
  },
  "Resilience": {
    "Retry": {
      "MaxRetryAttempts": 5,
      "InitialDelay": 2,
      "BackoffType": "Exponential"
    },
    "CircuitBreaker": {
      "FailureRatio": 0.5,
      "SamplingDuration": 30,
      "MinimumThroughput": 10
    }
  }
}
```

**Security Note**: Encrypt the `Password` field using Windows DPAPI or Azure Key Vault in production.

---

## 10. Deployment

### 10.1 Prerequisites

- Windows 10/11 or Windows Server 2019/2022
- .NET 9 Runtime (Desktop + ASP.NET Core)
- Administrator privileges (for Windows Service installation)

### 10.2 Installation Steps

#### Step 1: Publish the Service

```powershell
# Navigate to solution root
cd C:\path\to\MesMiddleware

# Publish self-contained Windows Service
dotnet publish src\MesMiddleware.Service -c Release -r win-x64 --self-contained -o publish\service

# Publish WPF Monitor
dotnet publish src\MesMiddleware.Monitor -c Release -r win-x64 --self-contained -o publish\monitor
```

#### Step 2: Install Windows Service

```powershell
# Run as Administrator
sc.exe create MesMiddlewareService binPath= "C:\publish\service\MesMiddleware.Service.exe" start= auto

# Configure recovery options (auto-restart on failure)
sc.exe failure MesMiddlewareService reset= 86400 actions= restart/5000/restart/10000/restart/30000

# Start the service
sc.exe start MesMiddlewareService

# Verify status
sc.exe query MesMiddlewareService
```

#### Step 3: Configure appsettings.Production.json

```powershell
cd C:\publish\service

# Edit configuration file
notepad appsettings.Production.json

# Update the following:
# - WebApi.BaseUrl: Production MES Cloud API URL
# - WebApi.Username: Middleware service account
# - WebApi.Password: Encrypted password
# - WebApi.MachineNumber: Actual machine identifier
# - WebApi.MachineIp: Actual machine IP address
```

#### Step 4: Verify Installation

```powershell
# Check service logs
Get-Content C:\publish\service\logs\middleware-*.log -Tail 50

# Test API endpoint
curl http://localhost:5100/health

# Expected response:
# {"status":"Healthy","checks":{...},"uptime":"00:01:23"}
```

#### Step 5: Deploy WPF Monitor

```powershell
# Copy to Program Files
Copy-Item -Path C:\publish\monitor\* -Destination "C:\Program Files\MesMiddleware\Monitor" -Recurse

# Create desktop shortcut
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:PUBLIC\Desktop\MES Monitor.lnk")
$Shortcut.TargetPath = "C:\Program Files\MesMiddleware\Monitor\MesMiddleware.Monitor.exe"
$Shortcut.Save()
```

---

### 10.3 Uninstallation

```powershell
# Stop and delete the service
sc.exe stop MesMiddlewareService
sc.exe delete MesMiddlewareService

# Remove files
Remove-Item -Path "C:\publish" -Recurse -Force
Remove-Item -Path "C:\Program Files\MesMiddleware" -Recurse -Force
```

---

### 10.4 Upgrading from Shared Memory Architecture

See [Section 12: Migration from Shared Memory Architecture](#12-migration-from-shared-memory-architecture) for complete migration guide.

**Quick Upgrade Steps**:
1. Backup existing SQLite database (`Data/queue.db`)
2. Stop old service: `sc.exe stop MesMiddlewareService`
3. Deploy new version (follow installation steps above)
4. Update equipment code to use HTTP POST API
5. Start new service: `sc.exe start MesMiddlewareService`
6. Verify with WPF Monitor and Swagger UI (`http://localhost:5100/swagger`)

---

## 11. Testing Strategy

### 11.1 Test Coverage Summary

**Total Tests**: 80 tests (100% passing)
- Service Tests: 54 tests
  - Contract Tests: 5 tests (JSON schema validation)
  - Integration Tests: 19 tests (HTTP endpoint, database, MES API)
  - Unit Tests: 30 tests (service logic, validators)
- Monitor Tests: 26 tests
  - StatusViewModel: 6 tests
  - HistoryViewModel: 8 tests
  - TrayIconService: 12 tests

**Test Coverage**: 85% (line coverage), 80% (branch coverage)

---

### 11.2 Test Categories

#### 11.2.1 Contract Tests (5 tests)

Validate JSON schema and data structure contracts.

**File**: `tests/MesMiddleware.Service.Tests/Contract/InspectionRecordContractTests.cs`

```csharp
[Fact]
public void InspectionRecord_ValidJson_DeserializesCorrectly()
{
    var json = """
    {
      "machineNumber": "MACHINE-01",
      "serialNumber": "SN001",
      "inspectionResult": "OK",
      "inspectionTime": "2025-11-20T10:30:45Z",
      "measurementData": {"diameter": 25.4}
    }
    """;

    var record = JsonSerializer.Deserialize<InspectionRecord>(json);

    Assert.NotNull(record);
    Assert.Equal("MACHINE-01", record.MachineNumber);
    Assert.Equal("OK", record.InspectionResult);
}
```

**Tests**:
- `InspectionRecord_ValidJson_DeserializesCorrectly`
- `InspectionRecord_MissingRequiredField_ValidationFails`
- `InspectionRecord_InvalidInspectionResult_ValidationFails`
- `ConnectionStatusDto_Serialization_PreservesAllFields`
- `UploadHistoryDto_Serialization_HandlesNullErrorMessage`

---

#### 11.2.2 Integration Tests (19 tests)

Test interactions between components (HTTP endpoints, database, external APIs).

**File**: `tests/MesMiddleware.Service.Tests/Integration/InspectionEndpointTests.cs`

```csharp
[Fact]
public async Task SubmitInspection_ValidData_Returns202Accepted()
{
    // Arrange
    var client = _factory.CreateClient();
    var payload = new InspectionRecord
    {
        MachineNumber = "TEST-01",
        SerialNumber = "SN001",
        InspectionResult = "OK",
        InspectionTime = DateTime.UtcNow
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/inspection/submit", payload);

    // Assert
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
}
```

**Key Test Scenarios**:
- ✅ Valid HTTP POST returns 202 Accepted
- ✅ Invalid JSON returns 400 Bad Request with validation errors
- ✅ Channel backpressure blocks when queue is full
- ✅ Failed uploads are saved to SQLite queue
- ✅ Hangfire retry jobs are scheduled correctly
- ✅ Status endpoint returns accurate statistics
- ✅ History endpoint filters by status parameter
- ✅ Manual retry triggers Hangfire job

---

#### 11.2.3 Unit Tests (30 tests)

Test individual service components in isolation.

**File**: `tests/MesMiddleware.Service.Tests/Unit/InspectionRecordValidatorTests.cs`

```csharp
[Fact]
public void Validate_EmptyMachineNumber_ReturnsError()
{
    // Arrange
    var validator = new InspectionRecordValidator();
    var record = new InspectionRecord
    {
        MachineNumber = "", // Invalid
        SerialNumber = "SN001",
        InspectionResult = "OK",
        InspectionTime = DateTime.UtcNow
    };

    // Act
    var result = validator.Validate(record);

    // Assert
    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.PropertyName == "MachineNumber");
}
```

**Key Test Scenarios**:
- ✅ Validators reject empty/null required fields
- ✅ Validators enforce max length constraints
- ✅ Validators accept valid enum values ("OK", "NG", "Recheck")
- ✅ InspectionChannelProcessor reads from channel correctly
- ✅ UploadQueueService saves and retrieves items from SQLite
- ✅ StatusViewModel updates properties when status changes
- ✅ HistoryViewModel filters data correctly

---

### 11.3 WPF Monitor Tests (26 tests)

**File**: `tests/MesMiddleware.Monitor.Tests/Unit/StatusViewModelTests.cs`

```csharp
[Fact]
public async Task RefreshStatus_ApiReturnsConnected_UpdatesStatusToGreen()
{
    // Arrange
    var mockApiClient = new Mock<IMiddlewareApiClient>();
    mockApiClient.Setup(x => x.GetConnectionStatusAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ConnectionStatusDto
        {
            Status = "Connected",
            TotalReceived = 100,
            SuccessfulUploads = 95,
            QueuedUploads = 5
        });

    var viewModel = new StatusViewModel(mockApiClient.Object, Mock.Of<ILogger<StatusViewModel>>());

    // Act
    await viewModel.RefreshStatusAsync();

    // Assert
    Assert.Equal("Connected", viewModel.ConnectionStatus);
    Assert.Equal("Green", viewModel.StatusColor);
    Assert.Equal(100, viewModel.TotalDataReceived);
}
```

**Test Scenarios**:
- ✅ Status updates to Green when Connected
- ✅ Status updates to Yellow when Retrying
- ✅ Status updates to Red when Disconnected/Error
- ✅ Auto-refresh starts and stops correctly
- ✅ History grid displays recent uploads
- ✅ Manual retry command triggers API call
- ✅ System tray icon changes color with status

---

### 11.4 Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific category
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML coverage report (requires ReportGenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
```

---

### 11.5 Test Data Management

**SQLite In-Memory Database** (for integration tests):
```csharp
services.AddDbContext<MiddlewareDbContext>(options =>
    options.UseSqlite("Data Source=:memory:"));
```

**WireMock.Net** (for external API mocking):
```csharp
var wireMockServer = WireMockServer.Start();
wireMockServer.Given(
    Request.Create().WithPath("/api/inspection").UsingPost()
).RespondWith(
    Response.Create().WithStatusCode(200)
);
```

---

## 12. Migration from Shared Memory Architecture

### 12.1 Architecture Comparison

| Aspect | Shared Memory (v1.0) | Web API (v2.0) |
|--------|---------------------|----------------|
| **Communication** | MemoryMappedFile + EventWaitHandle | HTTP POST + Channel Queue |
| **Equipment Integration** | C# P/Invoke, LabVIEW Call Library Node | HTTP client (any language) |
| **WPF Monitoring** | SQLite direct access | HTTP polling (/api/status) |
| **Queue Tier 1** | N/A | Channel<InspectionRecord> (in-memory) |
| **Queue Tier 2** | SQLite (only tier) | SQLite (persistent queue) |
| **Queue Tier 3** | Hangfire retry | Hangfire retry (unchanged) |
| **Service Hosting** | Microsoft.Extensions.Hosting | ASP.NET Core WebApplication |
| **API Documentation** | N/A | Swagger/OpenAPI |
| **Cross-Platform** | Windows-only | Windows-only (WPF limitation) |

---

### 12.2 Breaking Changes

#### 12.2.1 Equipment Integration

**Old (Shared Memory)**:
```csharp
// Equipment writes to shared memory
var mmf = MemoryMappedFile.CreateOrOpen("MES_INSPECTION_DATA", 10 * 1024 * 1024);
var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "MES_DATA_READY");

using var stream = mmf.CreateViewStream();
using var writer = new BinaryWriter(stream);
var json = JsonSerializer.Serialize(inspectionData);
var bytes = Encoding.UTF8.GetBytes(json);
writer.Write(bytes.Length);
writer.Write(bytes);

eventHandle.Set(); // Signal middleware
```

**New (HTTP API)**:
```csharp
// Equipment POSTs to HTTP endpoint
var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5100") };
var payload = new InspectionRecord
{
    MachineNumber = "MACHINE-01",
    SerialNumber = "SN001",
    InspectionResult = "OK",
    InspectionTime = DateTime.UtcNow
};

var response = await httpClient.PostAsJsonAsync("/api/inspection/submit", payload);
if (response.IsSuccessStatusCode)
{
    Console.WriteLine("Data submitted successfully");
}
```

**LabVIEW Integration** (old vs new):

**Old**: Required complex shared memory VIs with Windows API calls
**New**: Use built-in HTTP Client VIs

```
┌─────────────────────────────────────────┐
│ LabVIEW Block Diagram (New Approach)    │
│                                         │
│  [POST]──[URL: http://localhost:5100...│
│     │                                   │
│     └──[Body: JSON String]              │
│              │                          │
│         [Response Status Code]          │
│              │                          │
│         [202 = Success]                 │
└─────────────────────────────────────────┘
```

---

#### 12.2.2 WPF Monitor Changes

**Old (SQLite Direct Access)**:
```csharp
// WPF reads SQLite directly
public class MiddlewareApiClient
{
    private readonly string _databasePath;

    public async Task<ServiceStatus> GetStatusAsync()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        // ... query database ...
    }
}
```

**New (HTTP Polling)**:
```csharp
// WPF polls HTTP API
public class MiddlewareApiClient
{
    private readonly HttpClient _httpClient;

    public async Task<ConnectionStatusDto> GetConnectionStatusAsync()
    {
        var response = await _httpClient.GetAsync("/api/status");
        return await response.Content.ReadFromJsonAsync<ConnectionStatusDto>();
    }
}
```

**Benefits**:
- ✅ No SQLite locking conflicts
- ✅ Service and Monitor can run concurrently
- ✅ Decoupled architecture (easier testing)

---

### 12.3 Migration Checklist

#### Pre-Migration

- [ ] Backup existing SQLite database (`Data/queue.db`, `Data/hangfire.db`)
- [ ] Document current equipment integration code (C#/LabVIEW)
- [ ] Test equipment HTTP client implementation in dev environment
- [ ] Review current `appsettings.json` configuration

#### Migration Steps

- [ ] **Step 1**: Deploy new middleware service (v2.0) alongside old service (v1.0)
  - Run on different port: `http://localhost:5100` (new) vs old service
  - Keep both services running during transition

- [ ] **Step 2**: Update equipment code to use HTTP POST API
  - Replace shared memory write logic with HTTP client
  - Test with new service endpoint
  - Keep fallback to old shared memory (for rollback)

- [ ] **Step 3**: Update WPF Monitor application
  - Install new version from `publish\monitor`
  - Point to new service: `http://localhost:5100`
  - Verify real-time status updates

- [ ] **Step 4**: Validate data flow
  - Submit test inspection from equipment
  - Verify appears in WPF History tab
  - Check MES Cloud API receives data
  - Test offline queue (disconnect network, verify retry)

- [ ] **Step 5**: Stop old service
  - `sc.exe stop MesMiddlewareService` (old v1.0 service)
  - Monitor logs for 24 hours
  - If stable, uninstall old service

- [ ] **Step 6**: Cleanup
  - Remove old shared memory code from equipment
  - Archive old service binaries
  - Update documentation

---

### 12.4 Rollback Plan

If issues occur during migration:

1. **Stop new service**:
   ```powershell
   sc.exe stop MesMiddlewareService
   ```

2. **Restore old service**:
   ```powershell
   sc.exe start MesMiddlewareServiceOld
   ```

3. **Revert equipment code** to shared memory implementation

4. **Revert WPF Monitor** to old version (SQLite direct access)

5. **Restore database** from backup:
   ```powershell
   Copy-Item backup\queue.db Data\queue.db -Force
   ```

---

### 12.5 Equipment Integration Examples

#### C# Equipment Integration

**File**: `Equipment/CSharpExample/Program.cs`

```csharp
using System.Net.Http.Json;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:5100"),
    Timeout = TimeSpan.FromSeconds(5)
};

// Simulate inspection
var inspectionData = new
{
    machineNumber = "MACHINE-01",
    serialNumber = $"SN{DateTime.Now:yyyyMMddHHmmss}",
    inspectionResult = "OK",
    inspectionTime = DateTime.UtcNow,
    measurementData = new
    {
        diameter = 25.4,
        length = 100.0
    }
};

try
{
    var response = await httpClient.PostAsJsonAsync("/api/inspection/submit", inspectionData);

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine("✅ Data submitted successfully");
    }
    else
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"❌ Submission failed: {error}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Exception: {ex.Message}");
}
```

---

#### LabVIEW Equipment Integration

**LabVIEW HTTP POST VI**:

1. **Palette**: Web Services → HTTP Client → POST.vi
2. **Inputs**:
   - URL: `http://localhost:5100/api/inspection/submit`
   - Headers: `Content-Type: application/json`
   - Body: Flattened JSON string

**Example VI Block Diagram**:
```
┌──────────────────────────────────────────────────────────┐
│ [Cluster to JSON]──┬──[POST VI]──┬──[Status Code]        │
│  ├─ machineNumber  │             │      │                │
│  ├─ serialNumber   │             │   [202 = OK]          │
│  ├─ inspectionRes..│             │   [400 = Error]       │
│  ├─ inspectionTime │             │                       │
│  └─ measurementData│             │   [Response Body]     │
│                    │             │      │                │
│ [URL: http://...   │             │   [Parse JSON]        │
│  localhost:5100... │             │   [Error Message]     │
└──────────────────────────────────────────────────────────┘
```

**LabVIEW JSON Serialization**:
```
Cluster "Inspection Data"
├─ Machine Number (String)
├─ Serial Number (String)
├─ Inspection Result (String) ["OK", "NG", "Recheck"]
├─ Inspection Time (Timestamp)
└─ Measurement Data (Cluster)
    ├─ Diameter (DBL)
    └─ Length (DBL)

Convert to JSON using LabVIEW Flatten to JSON function:
{
  "machineNumber": "MACHINE-01",
  "serialNumber": "SN20251120001",
  "inspectionResult": "OK",
  "inspectionTime": "2025-11-20T10:30:45Z",
  "measurementData": {
    "diameter": 25.4,
    "length": 100.0
  }
}
```

---

### 12.6 Performance Comparison

| Metric | Shared Memory (v1.0) | Web API (v2.0) |
|--------|---------------------|----------------|
| **Write Latency** | < 5ms | < 100ms (HTTP overhead) |
| **Throughput** | 10,000+ writes/sec | 100+ requests/sec |
| **Reliability** | Process crash loses data | Channel + SQLite = reliable |
| **Equipment Complexity** | High (P/Invoke, unsafe code) | Low (standard HTTP) |
| **Multi-Language Support** | C#/C++ only | Any language with HTTP |
| **Monitoring** | SQLite locking issues | HTTP polling (no conflicts) |

**Recommendation**: Web API (v2.0) is preferred for reliability, maintainability, and cross-language support, despite slightly higher latency.

---

## Appendices

### Appendix A: Glossary

- **Channel**: .NET System.Threading.Channels bounded queue for async producer-consumer pattern
- **Circuit Breaker**: Polly resilience pattern that prevents calls to failing external services
- **DTO**: Data Transfer Object - simple object for transferring data between layers
- **Hangfire**: Background job scheduler for .NET
- **JWT**: JSON Web Token - standard for secure authentication
- **Kestrel**: Cross-platform web server for ASP.NET Core
- **MVVM**: Model-View-ViewModel - architectural pattern for WPF
- **Polly**: .NET resilience library for retry, circuit breaker, timeout policies
- **Swagger**: OpenAPI specification for REST API documentation
- **TDD**: Test-Driven Development - write tests before implementation

---

### Appendix B: Related Documentation

- **README.md**: Quickstart guide and troubleshooting
- **COVERAGE_ANALYSIS.md**: Detailed test-to-requirement mapping
- **MIGRATION_GUIDE.md**: Step-by-step migration from v1.0 to v2.0
- **specs/002-shared-memory-middleware/spec.md**: Original feature specification (archived)
- **specs/002-shared-memory-middleware/tasks.md**: Task breakdown and status
- **.archive/**: Archived shared memory implementation code

---

### Appendix C: Support and Contact

For issues and bug reports:
- **GitHub Issues**: (not applicable - internal project)
- **Email**: middleware-support@company.com
- **Documentation**: `docs/` folder in repository

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-15 | Development Team | Initial SRS for Shared Memory architecture |
| 2.0 | 2025-11-20 | Development Team | Complete Web API migration, consolidated documentation |

---

**End of Software Requirements Specification**
