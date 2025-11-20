# Feature Specification: MES Middleware Service (Web API Architecture)

**Feature Branch**: `002-shared-memory-middleware` → `003-web-api-middleware`
**Created**: 2025-11-11
**Updated**: 2025-11-20 (Migrated from Shared Memory to Web Service)
**Status**: In Progress - Migration to Web API
**Input**: User description: "Middleware service (M) for machine equipment data collection via HTTP REST API, with WPF monitoring UI and WebAPI integration"

## Architecture Change Notice

**MIGRATION**: This specification has been updated to reflect the architectural change from **Shared Memory IPC** to **Web Service (HTTP REST API)**.

**Previous Architecture**: Equipment → MemoryMappedFile → Middleware Service → MES Cloud API
**Current Architecture**: Equipment → HTTP POST → Middleware Service (Kestrel Web API) → MES Cloud API

See `MIGRATION_GUIDE.md` for detailed migration information.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Equipment Data Collection via HTTP API (Priority: P1)

Production operators run inspection equipment (blind hole detection, AOI, AVI, etc.) that submits inspection results via HTTP POST to the middleware service. The middleware service (M) automatically receives HTTP requests, validates data, queues to in-memory channel, and forwards to the MES Cloud WebAPI (W) without requiring operator intervention.

**Why this priority**: Core functionality that enables automated data collection from equipment using industry-standard HTTP REST API. This provides cross-platform compatibility, easier integration, and better testability compared to shared memory. This is the foundation for all subsequent features.

**Independent Test**: Can be fully tested by simulating equipment POSTing JSON data to `POST /api/inspection/submit`, verifying middleware accepts the request (202 Accepted), processes data via channel, and successfully uploads to MES Cloud API. Delivers automated equipment-to-cloud data pipeline.

**Acceptance Scenarios**:

1. **Given** equipment completes blind hole inspection, **When** equipment POSTs JSON inspection data to `POST /api/inspection/submit`, **Then** middleware validates data and responds with HTTP 202 Accepted within 100ms
2. **Given** middleware receives valid HTTP POST request, **When** middleware validates the JSON payload, **Then** middleware successfully deserializes inspection record with all mandatory fields (rowNo, procName, devName, userName, workClass, traceCode/lotNo) and writes to in-memory channel
3. **Given** middleware has queued inspection data in channel, **When** InspectionChannelProcessor reads from channel and calls MES Cloud WebAPI POST /api/inspection/upload, **Then** WebAPI responds with success (HTTP 200) and middleware logs successful upload
4. **Given** MES Cloud WebAPI is temporarily unavailable (HTTP 503), **When** middleware attempts upload, **Then** middleware queues data to SQLite database and retries with exponential backoff (2s, 4s, 8s, 16s, 32s) via Hangfire
5. **Given** equipment POSTs malformed JSON, **When** middleware validates the request, **Then** middleware responds with HTTP 400 Bad Request including detailed validation errors in response body

---

### User Story 2 - Real-Time Monitoring Dashboard (Priority: P2)

Production supervisors and IT operators need to monitor the middleware service status in real-time without logging into servers. The WPF desktop application polls the middleware HTTP API (`GET /api/status`) every 2 seconds to display live connection status to MES Cloud WebAPI, recent inspection upload history, and processing statistics.

**Why this priority**: Enables operators to diagnose data collection issues immediately without technical support. Reduces downtime by providing visibility into data pipeline health. HTTP polling provides simpler implementation than shared state files. Implements after core data collection (P1) works.

**Independent Test**: Can be tested by launching WPF application, verifying UI polls `GET /api/status` endpoint, displays connection status indicators (green=connected, red=disconnected), viewing scrolling log of recent uploads with timestamps and success/failure status. Delivers operational visibility.

**Acceptance Scenarios**:

1. **Given** middleware service is running on http://localhost:5100, **When** operator launches WPF application, **Then** application polls `GET /api/status` every 2 seconds and displays current connection status to MES Cloud WebAPI (connected/disconnected with last activity timestamp)
2. **Given** equipment submits 5 inspection records via HTTP POST, **When** operator views "Upload History" tab, **Then** WPF application calls `GET /api/status/history?limit=100` and displays list of 5 records with timestamps, trace codes, and success/failure indicators
3. **Given** middleware processes inspection data from channel, **When** operator views "Statistics" panel, **Then** application displays real-time counters: totalReceived, successfulUploads, queuedUploads, currentQueueSize (updated every 2s via polling)
4. **Given** operator wants to minimize application, **When** operator clicks minimize button, **Then** application hides to system tray with icon showing connection status (green=connected, red=disconnected, yellow=retrying)
5. **Given** application is in system tray, **When** MES Cloud upload fails and status changes to "Disconnected", **Then** system tray icon changes to red and shows notification balloon with error summary

---

### User Story 3 - Bidirectional Command & Control (Priority: P3) - NOT IMPLEMENTED

Production supervisors need to send commands from MES Cloud WebAPI to equipment (e.g., change inspection parameters, trigger calibration). MES Cloud calls middleware API, middleware exposes command API endpoint for equipment to poll, equipment executes commands and sends acknowledgment.

**Why this priority**: Enables remote control and configuration of equipment from central MES system. This is an enhancement after basic data collection works. Less critical than monitoring (P2) as equipment typically operates autonomously.

**STATUS**: ⚠️ **NOT IMPLEMENTED IN CURRENT ARCHITECTURE** - This feature will be designed in future iteration using HTTP long-polling or WebSocket for equipment command delivery.

**Proposed Architecture** (Future):
```
MES Cloud → POST /api/command/send → Middleware (stores command)
Equipment → GET /api/command/poll?equipmentId=XXX (long-polling) → Middleware returns pending command
Equipment → POST /api/command/ack → Middleware (acknowledges execution)
Middleware → POST to MES Cloud (forwards acknowledgment)
```

**Acceptance Scenarios** (Deferred):

1. **Given** MES Cloud sends command (POST /api/command/send), **When** middleware receives command, **Then** middleware stores command in queue and returns 202 Accepted
2. **Given** equipment polls for commands (GET /api/command/poll), **When** pending command exists, **Then** middleware returns command JSON within 1 second
3. **Given** equipment executes command successfully, **When** equipment POSTs acknowledgment (POST /api/command/ack), **Then** middleware logs success and forwards acknowledgment to MES Cloud
4. **Given** equipment fails to acknowledge command within 30 seconds, **When** timeout expires, **Then** middleware logs timeout error and notifies MES Cloud of command failure
5. **Given** operator views WPF "Command History" tab, **When** 3 commands were sent in last hour, **Then** application displays list of 3 commands with status (pending/success/failed/timeout)

---

### Edge Cases

- What happens when middleware service is not running and equipment tries to POST data? (Equipment receives HTTP connection refused error)
- How does middleware handle corrupted HTTP request body (invalid JSON, incomplete JSON)?
- What happens when multiple equipment instances POST to middleware API simultaneously? (HTTP server handles concurrent requests via thread pool)
- How does system handle middleware service crash and restart? (Recover pending uploads from SQLite queue via Hangfire)
- What happens when in-memory channel fills up (1000 items capacity)? (BoundedChannelFullMode.Wait causes equipment HTTP request to block until space available)
- How does middleware handle equipment POSTing data faster than MES Cloud can accept? (Backpressure: Channel blocks → Equipment HTTP waits → SQLite queue + Hangfire retry)
- What happens when operator force-closes WPF application while monitoring? (No impact - WPF is read-only polling client)
- How does system handle clock skew between equipment, middleware, and MES Cloud servers? (Timestamps use UTC, clock sync via NTP recommended)
- What happens when equipment POSTs data in different JSON schema version? (FluentValidation returns HTTP 400 with field-level errors)
- What happens when network connection to MES Cloud is unstable? (Retry logic with exponential backoff, SQLite persistence, Hangfire background jobs)
- What happens when equipment sends duplicate data (same traceCode/rowNo)? (Middleware accepts all POSTs, MES Cloud handles deduplication)

## Requirements *(mandatory)*

### Functional Requirements

#### HTTP API Communication (Replaces Shared Memory)

- **FR-001**: Middleware MUST expose HTTP REST API endpoint `POST /api/inspection/submit` for equipment to submit inspection data
- **FR-002**: Middleware MUST respond to equipment HTTP POST requests within 100ms with HTTP 202 Accepted (or 400 Bad Request if validation fails)
- **FR-003**: Middleware MUST accept JSON-formatted inspection data in HTTP request body with UTF-8 encoding (Content-Type: application/json)
- **FR-004**: Middleware MUST validate and deserialize HTTP request JSON into InspectionRecord objects matching MES Cloud WebAPI contract
- **FR-005**: Middleware MUST queue validated inspection data to in-memory bounded channel (System.Threading.Channels) with capacity 1000 items
- **FR-006**: Middleware MUST run embedded Kestrel web server on configurable URLs (default: http://localhost:5100, https://localhost:5101)
- **FR-007**: Middleware MUST handle concurrent HTTP requests from multiple equipment instances using ASP.NET Core thread pool
- **FR-008**: Middleware MUST return HTTP 400 Bad Request with detailed FluentValidation errors when inspection data is invalid

#### Data Upload & Integration

- **FR-009**: Middleware MUST process inspection data from in-memory channel via background service (InspectionChannelProcessor)
- **FR-010**: Middleware MUST POST inspection data to MES Cloud WebAPI endpoint (POST /api/inspection/upload) with authentication token in header
- **FR-011**: Middleware MUST validate inspection data completeness before upload (required fields: rowNo, procName, devName, userName, workClass, traceCode OR lotNo) - validation happens at HTTP API layer via FluentValidation
- **FR-012**: Middleware MUST implement retry logic with exponential backoff (2s, 4s, 8s, 16s, 32s) for transient MES Cloud WebAPI failures (HTTP 5xx, network timeout) via Hangfire background jobs
- **FR-013**: Middleware MUST queue failed uploads to local SQLite database when MES Cloud WebAPI is unavailable, with Hangfire scheduling automatic retries
- **FR-014**: Middleware MUST preserve upload order (FIFO) when processing queued data from in-memory channel (SQLite queue processes independently with retry logic)
- **FR-015**: Middleware MUST authenticate with MES Cloud WebAPI on startup (obtain token via POST /api/auth/login with machine number and IP)
- **FR-016**: Middleware MUST refresh authentication token automatically before expiration (token lifetime typically 8 hours) via TokenService

#### Real-Time Monitoring UI (WPF)

- **FR-017**: WPF application MUST poll middleware HTTP API (`GET /api/status`) every 2 seconds to retrieve current service status
- **FR-018**: WPF application MUST display real-time connection status to MES Cloud WebAPI (connected/disconnected/retrying with last activity timestamp) from API response
- **FR-019**: WPF application MUST display inspection upload history by calling `GET /api/status/history?limit=100` with columns: timestamp, trace code/lot number, equipment name, upload status (success/failed/pending), error message (if failed)
- **FR-020**: WPF application MUST display processing statistics: totalReceived, successfulUploads, queuedUploads, currentQueueSize (from `GET /api/status` response)
- **FR-021**: WPF application MUST support minimizing to Windows system tray with icon reflecting connection status (green=connected, red=disconnected, yellow=retrying)
- **FR-022**: WPF application MUST show Windows notification balloon when critical errors occur (authentication failure, MES Cloud WebAPI unreachable for >5 minutes, based on status polling)
- **FR-023**: WPF application MUST auto-refresh UI data every 2 seconds via HTTP polling to display near real-time status without manual refresh
- **FR-024**: WPF application MUST support manual retry of failed uploads by calling `POST /api/status/queue/{id}/retry` (select failed record and click "Retry" button)

#### Service Management

- **FR-025**: Middleware MUST run as Windows background service (WindowsService) with embedded Kestrel web server that starts automatically on system boot
- **FR-026**: Middleware service MUST support graceful shutdown (complete in-flight channel processing, close HTTP server, persist queue to SQLite)
- **FR-027**: Middleware service MUST log all operations (HTTP requests, channel processing, upload attempts, errors) to structured log files with daily rotation (Serilog)
- **FR-028**: Middleware configuration MUST be stored in appsettings.json with settings for: HTTP URLs, InspectionChannel capacity, MES Cloud WebAPI base URL, machine number/IP, retry policies, log levels
- **FR-029**: Middleware MUST expose health check endpoint (`GET /health`) returning service status, in-memory channel depth, SQLite queue size, MES Cloud WebAPI connectivity
- **FR-030**: Middleware MUST expose Swagger UI documentation at `/swagger` endpoint (development mode only) for API testing and exploration

#### Error Handling & Resilience

- **FR-031**: Middleware MUST log detailed error context when HTTP request processing fails (endpoint, status code, request body preview)
- **FR-032**: Middleware MUST continue operating after individual upload failures (one failed upload does not block subsequent channel processing)
- **FR-033**: Middleware MUST detect and log JSON deserialization errors with partial data preview (first 200 characters) for debugging, returning HTTP 400 Bad Request
- **FR-034**: Middleware MUST detect MES Cloud WebAPI authentication failures and alert operator via WPF UI notification (status polling detects "Disconnected")
- **FR-035**: Middleware MUST implement circuit breaker pattern for MES Cloud WebAPI calls (open circuit after 10 consecutive failures, half-open retry after 60 seconds) via Polly resilience policies
- **FR-036**: Middleware MUST implement backpressure handling: when in-memory channel is full (1000 items), equipment HTTP POST requests wait (BoundedChannelFullMode.Wait) until space available

### Key Entities

- **Middleware Service (M)**: Windows background service with embedded Kestrel web server that bridges equipment (via HTTP REST API) and MES Cloud WebAPI (via HTTP). Runs continuously, exposes API endpoints for equipment and WPF monitoring, processes data via in-memory channel, authenticates with MES Cloud, uploads inspection data, manages retry queue with Hangfire.

- **In-Memory Channel**: Bounded queue (`Channel<InspectionRecord>`) for buffering inspection data between HTTP API layer and background processor. Capacity: 1000 items, full mode: Wait (backpressure). Provides fast, thread-safe communication between InspectionController and InspectionChannelProcessor.

- **Inspection Data Message**: JSON object POSTed by equipment via HTTP to `POST /api/inspection/submit`, containing complete inspection record (equipment ID, operator info, trace codes, paramData, benchmarks, otherData). Matches InspectionRecord model and MES Cloud WebAPI contract.

- **Upload Queue Entry**: Persistent record of failed upload attempts stored in SQLite database (QueuedUpload table). Contains inspection data JSON, retry count, next retry timestamp, last error message, status (Pending/Retrying/Failed). Processed by Hangfire background jobs with exponential backoff.

- **Connection Status**: Real-time state of middleware's connectivity to MES Cloud WebAPI. States: Connected (last upload <30s ago), Disconnected (no response for >30s), Retrying (exponential backoff in progress). Exposed via `GET /api/status` endpoint.

- **WPF Monitoring Application**: Desktop user interface for operators to monitor middleware health, view upload history, inspect processing statistics, and manually retry failed uploads. Polls middleware HTTP API (`GET /api/status`, `GET /api/status/history`) every 2 seconds for near real-time updates.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Equipment operators experience zero manual data entry - inspection data automatically flows from equipment to MES Cloud within 3 seconds of HTTP POST
- **SC-002**: Middleware responds to equipment HTTP POST requests within 100ms (99th percentile latency) with 202 Accepted
- **SC-003**: Middleware successfully uploads 95% of inspection records to MES Cloud on first attempt under normal network conditions
- **SC-004**: Middleware recovers from MES Cloud WebAPI outages without data loss - all queued data uploads successfully after recovery (SQLite + Hangfire)
- **SC-005**: Operators can diagnose data pipeline issues within 30 seconds by viewing WPF monitoring dashboard (connection status via HTTP polling, recent errors, upload history)
- **SC-006**: Middleware service uptime exceeds 99.5% (less than 4 hours downtime per month)
- **SC-007**: System handles 100+ inspections per hour per equipment without backlog accumulation (in-memory channel processes data immediately)
- **SC-008**: Bidirectional command latency (MES Cloud → Equipment) under 5 seconds (P3 deferred - future implementation)
- **SC-009**: Middleware gracefully handles equipment JSON schema changes (FluentValidation returns HTTP 400 with field-level errors, logs warning)
- **SC-010**: WPF application launches in under 3 seconds, connects to middleware HTTP API (http://localhost:5100), and displays current status without requiring user configuration

### Assumptions

- Equipment can make HTTP POST requests (supports HttpClient or similar) with well-formed UTF-8 JSON (InspectionRecord schema)
- Equipment and middleware service can communicate over HTTP (localhost or network, ports accessible)
- Middleware service has network access to MES Cloud WebAPI server (firewall allows HTTP/HTTPS outbound)
- Windows version supports .NET 9 and Kestrel web server (Windows 10+, Server 2016+)
- Only one middleware instance runs per machine (single instance listening on configured ports)
- Equipment inspection rate is bounded (max 100+ inspections/hour, in-memory channel handles bursts up to 1000 items)
- Operators have Windows desktop access to launch WPF monitoring application (connects to http://localhost:5100)
- In-memory channel capacity (1000 items) is sufficient for buffering during temporary MES Cloud outages
- Clock synchronization between equipment, middleware, and MES Cloud is within 5 minutes (NTP configured, UTC timestamps)
- MES Cloud WebAPI authentication token lifetime is at least 4 hours (allows for scheduled token refresh via TokenService)
