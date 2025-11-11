# Feature Specification: Shared Memory Middleware Service

**Feature Branch**: `002-shared-memory-middleware`
**Created**: 2025-11-11
**Status**: Draft
**Input**: User description: "Shared memory middleware service (M) for machine equipment data collection via Windows shared memory, with WPF monitoring UI and WebAPI integration"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Equipment Data Collection via Shared Memory (Priority: P1)

Production operators run inspection equipment (blind hole detection, AOI, AVI, etc.) that writes inspection results to Windows shared memory. The middleware service (M) automatically detects new data, reads it from shared memory, and forwards it to the WebAPI (W) without requiring operator intervention.

**Why this priority**: Core functionality that enables automated data collection from legacy/third-party equipment that cannot directly call HTTP APIs. Without this, no data flows from equipment to MES system. This is the foundation for all subsequent features.

**Independent Test**: Can be fully tested by simulating equipment writing JSON data to shared memory, verifying middleware detects the event, reads the data correctly, and successfully POSTs to WebAPI. Delivers automated equipment-to-cloud data pipeline.

**Acceptance Scenarios**:

1. **Given** equipment completes blind hole inspection, **When** equipment writes JSON inspection data to shared memory segment "MES_INSPECTION_DATA", **Then** middleware detects the change within 1 second and triggers data upload event
2. **Given** middleware detects new shared memory data, **When** middleware reads the JSON payload, **Then** middleware successfully deserializes inspection record with all mandatory fields (rowNo, procName, devName, userName, workClass, traceCode/lotNo)
3. **Given** middleware has valid deserialized inspection data, **When** middleware calls WebAPI POST /api/inspection/upload, **Then** WebAPI responds with success (HTTP 200) and middleware logs successful upload
4. **Given** WebAPI is temporarily unavailable (HTTP 503), **When** middleware attempts upload, **Then** middleware queues data locally and retries with exponential backoff (2s, 4s, 8s, 16s, 32s)
5. **Given** equipment writes malformed JSON to shared memory, **When** middleware attempts to deserialize, **Then** middleware logs parsing error with details and notifies operator via UI error indicator

---

### User Story 2 - Real-Time Monitoring Dashboard (Priority: P2)

Production supervisors and IT operators need to monitor the middleware service status in real-time without logging into servers. The WPF desktop application displays live connection status to WebAPI, recent inspection upload history, and shared memory activity logs.

**Why this priority**: Enables operators to diagnose data collection issues immediately without technical support. Reduces downtime by providing visibility into data pipeline health. Implements after core data collection (P1) works.

**Independent Test**: Can be tested by launching WPF application, verifying UI displays connection status indicators (green=connected, red=disconnected), viewing scrolling log of recent uploads with timestamps and success/failure status. Delivers operational visibility.

**Acceptance Scenarios**:

1. **Given** middleware service is running, **When** operator launches WPF application, **Then** application displays current connection status to WebAPI (connected/disconnected with last ping timestamp)
2. **Given** equipment uploads 5 inspection records, **When** operator views "Upload History" tab, **Then** application displays list of 5 records with timestamps, trace codes, and success/failure indicators
3. **Given** shared memory receives new data from equipment, **When** operator views "Shared Memory Activity" tab, **Then** application displays real-time log entry showing data size, timestamp, and processing status
4. **Given** operator wants to minimize application, **When** operator clicks minimize button, **Then** application hides to system tray with icon showing connection status (green/red/yellow)
5. **Given** application is in system tray, **When** data upload fails, **Then** system tray icon changes to red and shows notification balloon with error summary

---

### User Story 3 - Bidirectional Command & Control (Priority: P3)

Production supervisors need to send commands from WebAPI to equipment (e.g., change inspection parameters, trigger calibration). WebAPI writes commands to shared memory, middleware detects them, and equipment reads the commands to execute operations.

**Why this priority**: Enables remote control and configuration of equipment from central MES system. This is an enhancement after basic data collection works. Less critical than monitoring (P2) as equipment typically operates autonomously.

**Independent Test**: Can be tested by calling WebAPI POST /api/equipment/command with JSON command payload, verifying middleware writes to shared memory segment "MES_EQUIPMENT_CMD", simulating equipment reading command and executing action. Delivers bidirectional communication.

**Acceptance Scenarios**:

1. **Given** WebAPI receives command request (POST /api/equipment/command), **When** WebAPI writes command JSON to shared memory, **Then** middleware detects command within 1 second and logs "Command received from WebAPI"
2. **Given** middleware detects new command in shared memory, **When** middleware writes command to equipment-readable shared memory segment, **Then** equipment receives command and acknowledges execution via response segment
3. **Given** equipment acknowledges command execution, **When** middleware reads acknowledgment from shared memory, **Then** middleware POSTs acknowledgment back to WebAPI (POST /api/equipment/command/ack)
4. **Given** equipment fails to acknowledge command within 30 seconds, **When** middleware timeout expires, **Then** middleware logs timeout error and notifies WebAPI of command failure
5. **Given** operator views WPF "Command History" tab, **When** 3 commands were sent in last hour, **Then** application displays list of 3 commands with status (pending/success/failed/timeout)

---

### Edge Cases

- What happens when shared memory segment does not exist (first run or equipment not started)?
- How does middleware handle corrupted shared memory data (invalid JSON, incomplete writes)?
- What happens when multiple equipment instances write to same shared memory segment simultaneously?
- How does system handle middleware service crash and restart (recover pending uploads)?
- What happens when shared memory fills up (insufficient size for large inspection data)?
- How does middleware handle equipment writing data faster than WebAPI can accept (backpressure)?
- What happens when operator force-closes WPF application while upload in progress?
- How does system handle clock skew between equipment, middleware, and WebAPI servers?
- What happens when equipment writes data in different JSON schema version?

## Requirements *(mandatory)*

### Functional Requirements

#### Shared Memory Communication

- **FR-001**: Middleware MUST monitor Windows shared memory segment named "MES_INSPECTION_DATA" for new inspection data from equipment
- **FR-002**: Middleware MUST detect shared memory changes within 1 second of equipment writing data (event-based notification, not polling)
- **FR-003**: Middleware MUST read JSON-formatted inspection data from shared memory with UTF-8 encoding
- **FR-004**: Middleware MUST deserialize shared memory JSON into inspection record objects matching WebAPI contract (InspectionDataRequest schema)
- **FR-005**: Middleware MUST support bidirectional shared memory communication: reading inspection data from equipment and writing commands to equipment
- **FR-006**: Middleware MUST create shared memory segments on startup if they do not exist, with configurable size (default 10MB per segment)
- **FR-007**: Middleware MUST implement mutual exclusion (mutex/semaphore) to prevent concurrent access conflicts when reading/writing shared memory

#### Data Upload & Integration

- **FR-008**: Middleware MUST POST inspection data to WebAPI endpoint (POST /api/inspection/upload) with authentication token in header
- **FR-009**: Middleware MUST validate inspection data completeness before upload (required fields: rowNo, procName, devName, userName, workClass, traceCode OR lotNo)
- **FR-010**: Middleware MUST implement retry logic with exponential backoff (2s, 4s, 8s, 16s, 32s) for transient WebAPI failures (HTTP 5xx, network timeout)
- **FR-011**: Middleware MUST queue failed uploads to local SQLite database when WebAPI is unavailable for more than 5 minutes
- **FR-012**: Middleware MUST preserve upload order (FIFO) when processing queued data after WebAPI recovery
- **FR-013**: Middleware MUST authenticate with WebAPI on startup (obtain token via POST /api/auth/login with machine number and IP)
- **FR-014**: Middleware MUST refresh authentication token automatically before expiration (token lifetime typically 8 hours)

#### Real-Time Monitoring UI (WPF)

- **FR-015**: WPF application MUST display real-time connection status to WebAPI (connected/disconnected/retrying with last activity timestamp)
- **FR-016**: WPF application MUST display inspection upload history with columns: timestamp, trace code/lot number, equipment name, upload status (success/failed/pending), error message (if failed)
- **FR-017**: WPF application MUST display shared memory activity log with columns: timestamp, event type (read/write), data size (bytes), processing status
- **FR-018**: WPF application MUST support minimizing to Windows system tray with icon reflecting connection status (green=connected, red=disconnected, yellow=retrying)
- **FR-019**: WPF application MUST show Windows notification balloon when critical errors occur (authentication failure, shared memory access error, WebAPI unreachable for >5 minutes)
- **FR-020**: WPF application MUST auto-refresh UI data every 2 seconds to display near real-time status without manual refresh
- **FR-021**: WPF application MUST support manual retry of failed uploads (select failed record and click "Retry" button)

#### Service Management

- **FR-022**: Middleware MUST run as Windows background service that starts automatically on system boot
- **FR-023**: Middleware service MUST support graceful shutdown (complete in-flight uploads before exiting, persist queue to disk)
- **FR-024**: Middleware service MUST log all operations (shared memory events, upload attempts, errors) to structured log files with daily rotation
- **FR-025**: Middleware configuration MUST be stored in appsettings.json with settings for: shared memory segment names/sizes, WebAPI base URL, machine number/IP, retry policies, log levels
- **FR-026**: Middleware MUST expose health check endpoint (HTTP GET /health) returning service status, shared memory availability, WebAPI connectivity

#### Error Handling & Resilience

- **FR-027**: Middleware MUST log detailed error context when shared memory read fails (segment name, error code, data size attempted)
- **FR-028**: Middleware MUST continue operating after individual upload failures (one failed upload does not block subsequent uploads)
- **FR-029**: Middleware MUST detect and log JSON deserialization errors with partial data preview (first 200 characters) for debugging
- **FR-030**: Middleware MUST detect WebAPI authentication failures and alert operator via UI notification
- **FR-031**: Middleware MUST implement circuit breaker pattern for WebAPI calls (open circuit after 10 consecutive failures, half-open retry after 60 seconds)

### Key Entities

- **Middleware Service (M)**: Windows background service that bridges equipment (via shared memory) and WebAPI (via HTTP). Runs continuously, monitors shared memory events, authenticates with WebAPI, uploads inspection data, manages retry queue, and exposes monitoring endpoints.

- **Shared Memory Segment**: Named Windows shared memory block (MemoryMappedFile) used for inter-process communication between equipment and middleware. Contains JSON-formatted inspection data written by equipment, read by middleware. Typical size 10MB, named "MES_INSPECTION_DATA" for data upload and "MES_EQUIPMENT_CMD" for commands.

- **Inspection Data Message**: JSON object written by equipment to shared memory, containing complete inspection record (equipment ID, operator info, trace codes, paramData, benchmarks, otherData). Matches WebAPI InspectionDataRequest schema.

- **Upload Queue Entry**: Persistent record of failed upload attempts stored in SQLite database. Contains inspection data JSON, retry count, next retry timestamp, last error message. Processed by background job with exponential backoff.

- **Connection Status**: Real-time state of middleware's connectivity to WebAPI. States: Connected (last ping <30s ago), Disconnected (no response for >30s), Retrying (exponential backoff in progress), Authentication Failed (invalid token/IP mismatch).

- **WPF Monitoring Application**: Desktop user interface for operators to monitor middleware health, view upload history, inspect shared memory activity, and manually retry failed uploads. Connects to middleware service via local HTTP API or shared state file.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Equipment operators experience zero manual data entry - inspection data automatically flows from equipment to MES within 3 seconds of inspection completion
- **SC-002**: Middleware detects new shared memory data within 1 second of equipment writing (99th percentile latency)
- **SC-003**: Middleware successfully uploads 95% of inspection records on first attempt under normal network conditions
- **SC-004**: Middleware recovers from WebAPI outages without data loss - all queued data uploads successfully after WebAPI recovery
- **SC-005**: Operators can diagnose data pipeline issues within 30 seconds by viewing WPF monitoring dashboard (connection status, recent errors, upload history)
- **SC-006**: Middleware service uptime exceeds 99.5% (less than 4 hours downtime per month)
- **SC-007**: System handles 100 inspections per hour per equipment without backlog accumulation
- **SC-008**: Bidirectional command latency (WebAPI → Equipment) under 2 seconds (P1+P3 combined)
- **SC-009**: Middleware gracefully handles equipment JSON schema changes (logs warning, attempts best-effort parsing, alerts operator)
- **SC-010**: WPF application launches in under 3 seconds and displays current status without requiring user configuration

### Assumptions

- Equipment writes well-formed UTF-8 JSON to shared memory (JSON schema matches WebAPI InspectionDataRequest format)
- Equipment and middleware service run on same Windows machine (shared memory accessible to both processes)
- Middleware service has network access to WebAPI server (firewall allows HTTP/HTTPS outbound)
- Windows version supports MemoryMappedFile and EventWaitHandle APIs (Windows 10+, Server 2016+)
- Only one middleware instance runs per machine (no multi-instance coordination needed)
- Equipment inspection rate is bounded (max 100 inspections/hour, allowing time for upload retry)
- Operators have Windows desktop access to launch WPF monitoring application
- Shared memory size (10MB default) is sufficient for largest inspection records (typically 10-50KB per record)
- Clock synchronization between equipment and middleware is within 5 minutes (NTP configured)
- WebAPI authentication token lifetime is at least 4 hours (allows for scheduled token refresh)
