# Quickstart: MES Middleware Service

**Feature**: Shared Memory Middleware for Equipment Data Collection
**Version**: 1.0.0
**Platform**: Windows .NET 9

## Overview

The MES Middleware Service bridges equipment inspection data (via Windows shared memory) to the MES WebAPI, providing automated data collection with offline queueing and retry logic.

**Architecture**:
```
Equipment ’ Shared Memory ’ Middleware Service ’ MES WebAPI
                               “
                          SQLite Queue (offline resilience)
                               “
                          WPF Monitor (real-time visibility)
```

---

## Prerequisites

1. **Operating System**: Windows 10/11 or Windows Server 2016+
2. **.NET Runtime**: .NET 9 Runtime ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
3. **Equipment Software**: Must write JSON data to shared memory segment `MES_INSPECTION_DATA`
4. **Network Access**: HTTPS connectivity to MES WebAPI

---

## Quick Setup

### 1. Install Middleware Service

```powershell
# Clone or extract the middleware service
cd C:\MesMiddleware

# Build the service (Release mode, self-contained)
dotnet publish src\MesMiddleware.Service\MesMiddleware.Service.csproj -c Release -r win-x64 --self-contained

# Install as Windows Service (requires Administrator)
.\install-service.ps1
```

The installer will:
- Create Windows Service named `MesMiddlewareService`
- Configure automatic startup
- Set up restart-on-failure recovery

### 2. Configure Service

Edit `appsettings.json` in the service directory:

```json
{
  "WebApi": {
    "BaseUrl": "https://your-mes-api.company.com",
    "MachineNumber": "MACHINE001",
    "MachineIp": "192.168.1.100",
    "Username": "middleware_user",
    "Password": "your_password_here"
  },
  "SharedMemory": {
    "InspectionDataSegmentName": "MES_INSPECTION_DATA",
    "SegmentSize": 10485760
  }
}
```

**Key Configuration Parameters**:

| Parameter | Description | Default | Required |
|-----------|-------------|---------|----------|
| `WebApi.BaseUrl` | MES WebAPI endpoint URL | `http://localhost:5000` | Yes |
| `WebApi.MachineNumber` | Unique machine identifier | `MACHINE001` | Yes |
| `WebApi.MachineIp` | Machine IP address | `192.168.1.100` | Yes |
| `WebApi.Username` | API authentication username | - | Yes |
| `WebApi.Password` | API authentication password | - | Yes |
| `SharedMemory.InspectionDataSegmentName` | Shared memory segment name | `MES_INSPECTION_DATA` | Yes |
| `SharedMemory.SegmentSize` | Max shared memory size (bytes) | `10485760` (10MB) | No |
| `Queue.DatabasePath` | SQLite database path for offline queue | `Data/queue.db` | No |

### 3. Start Service

```powershell
# Start service
Start-Service -Name MesMiddlewareService

# Check service status
Get-Service -Name MesMiddlewareService

# View logs (real-time)
Get-Content logs\middleware-*.log -Tail 50 -Wait
```

### 4. Install WPF Monitoring Dashboard (Optional)

```powershell
# Build WPF Monitor
dotnet publish src\MesMiddleware.Monitor\MesMiddleware.Monitor.csproj -c Release -r win-x64 --self-contained

# Run monitor
.\src\MesMiddleware.Monitor\bin\Release\net9.0-windows\win-x64\publish\MesMiddleware.Monitor.exe
```

The WPF Monitor provides:
- Real-time service status (Connected/Disconnected/Retrying)
- Upload history (recent 50 uploads)
- Queue status (pending, retrying, failed counts)
- System tray integration

---

## Data Flow

### Equipment ’ Middleware

Equipment software writes JSON to shared memory:

1. **Create/Open MemoryMappedFile**: `MES_INSPECTION_DATA` (10MB)
2. **Acquire Mutex**: `MES_INSPECTION_DATA_MUTEX` (exclusive access)
3. **Write JSON**: Format below, UTF-8 encoded
4. **Signal EventWaitHandle**: `MES_DATA_READY`
5. **Release Mutex**

**JSON Schema** (InspectionRecord):

```json
{
  "rowNo": "12345",
  "procName": "Final Inspection",
  "devName": "VISION_SYSTEM_01",
  "userName": "OP001",
  "workClass": "A",
  "traceCode": "TR2025011100001",
  "lotNo": null,
  "inspectionTime": "2025-11-11T14:30:00Z",
  "paramData": [
    {"name": "Dimension_X", "value": "100.5", "unit": "mm", "status": "OK"},
    {"name": "Dimension_Y", "value": "50.2", "unit": "mm", "status": "OK"}
  ],
  "benchmarks": [
    {"name": "Dimension_X", "upperLimit": "101.0", "lowerLimit": "99.0", "unit": "mm"},
    {"name": "Dimension_Y", "upperLimit": "51.0", "lowerLimit": "49.0", "unit": "mm"}
  ],
  "otherData": [
    {"name": "Temperature", "value": "25.3", "unit": "°C"},
    {"name": "Humidity", "value": "45.2", "unit": "%"}
  ]
}
```

**Required Fields**:
- `rowNo`: Unique inspection record ID
- `procName`: Process name
- `devName`: Device/equipment name
- `userName`: Operator username
- `workClass`: Work classification
- `traceCode` OR `lotNo`: Product identifier (at least one required)

**Optional Arrays**:
- `paramData`: Measured parameters
- `benchmarks`: Specification limits
- `otherData`: Additional metadata

### Middleware ’ WebAPI

Middleware automatically:

1. **Detects Shared Memory Changes**: Within 1 second via `EventWaitHandle`
2. **Validates JSON**: FluentValidation rules
3. **Authenticates**: POST `/api/auth/login` (8-hour token cache)
4. **Uploads Data**: POST `/api/inspection/upload` with `accessToken` header
5. **Queues on Failure**: SQLite database + Hangfire retry (exponential backoff: 2s, 4s, 8s, 16s, 32s)

**WebAPI Endpoints Expected**:

| Endpoint | Method | Purpose | Headers |
|----------|--------|---------|---------|
| `/api/auth/login` | POST | Authenticate and get token | - |
| `/api/inspection/upload` | POST | Upload inspection data | `accessToken: {token}` |

**Request Body** (authentication):
```json
{
  "machineNumber": "MACHINE001",
  "ip": "192.168.1.100",
  "userName": "middleware_user",
  "password": "your_password"
}
```

**Response** (authentication):
```json
{
  "success": true,
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 28800
}
```

---

## Troubleshooting

### Service Won't Start

**Check logs**:
```powershell
Get-Content logs\middleware-*.log -Tail 100
```

**Common issues**:
1. **Shared memory not found**: Equipment software not running
   - Expected: "Degraded" status, service continues running
   - Action: Start equipment software

2. **WebAPI unreachable**: Network connectivity or wrong BaseUrl
   - Expected: Data queued to SQLite, retries automatically
   - Action: Check `appsettings.json` ’ `WebApi.BaseUrl`

3. **Authentication failure**: Wrong credentials
   - Expected: HTTP 401 errors in logs
   - Action: Verify `Username` and `Password` in config

4. **Database locked**: SQLite database in use
   - Expected: "Database is locked" errors
   - Action: Stop service, delete `Data/queue.db.wal` and `Data/queue.db-shm`, restart

### Data Not Uploading

**Verify shared memory**:
```csharp
// Test equipment writes to shared memory
using var segment = MemoryMappedFile.CreateOrOpen("MES_INSPECTION_DATA", 10485760);
using var eventSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "MES_DATA_READY");
// Write test JSON...
eventSignal.Set(); // Signal middleware
```

**Check middleware logs** for:
- "Received inspection data from equipment" (detection)
- "Successfully uploaded inspection data" (upload success)
- "Queued upload for later retry" (WebAPI failure)

### Monitor UI Shows "Disconnected"

1. **Service not running**: Start `MesMiddlewareService`
2. **Wrong database path**: Monitor looks for `Data/queue.db` relative to service
3. **Permissions**: Monitor needs read access to service database

---

## Performance Metrics

**Normal Operation**:
- Detection latency: < 1 second (shared memory ’ middleware)
- Upload latency: < 3 seconds (middleware ’ WebAPI)
- Throughput: 100+ inspections/hour
- Queue depth: 0 (no failures)

**Degraded Operation** (WebAPI offline):
- Queue growth rate: Matches inspection rate
- Retry attempts: 5 per item (exponential backoff)
- Recovery time: Automatic upon WebAPI restoration

---

## Uninstall

```powershell
# Stop service
Stop-Service -Name MesMiddlewareService

# Remove service (requires Administrator)
sc.exe delete MesMiddlewareService

# Clean up files (optional)
Remove-Item C:\MesMiddleware -Recurse -Force
```

---

## Support

**Logs Location**: `logs/middleware-YYYYMMDD.log` (7-day retention)
**Database Location**: `Data/queue.db` (offline queue)
**Configuration**: `appsettings.json` (service directory)

**Common Log Patterns**:
- `[INF] Starting MES Middleware Service` - Service started
- `[INF] Received inspection data from equipment` - Shared memory detection
- `[INF] Successfully uploaded inspection data` - WebAPI upload success
- `[WRN] Queued upload for later retry` - WebAPI failure (offline queue activated)
- `[ERR] Failed to upload inspection data` - Upload error (see details)

For production deployment, use `appsettings.Production.json` with appropriate WebAPI URLs and log levels.
