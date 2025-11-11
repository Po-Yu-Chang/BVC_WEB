# Quickstart: MES Trace Data Integration

**Feature**: MES Trace Data Integration
**Created**: 2025-11-11
**Prerequisites**: .NET 9 SDK, Windows 10+/Server 2019+, MES system access

## Overview

This guide walks through setting up, testing, and deploying the MES integration Web API that enables blind hole detection equipment to authenticate and upload inspection data to the Cimforce Trace Management System.

---

## 1. Development Environment Setup

### Install Prerequisites

```powershell
# Check .NET 9 SDK installation
dotnet --version
# Should output: 9.0.xxx

# If not installed, download from https://dotnet.microsoft.com/download/dotnet/9.0
```

### Clone and Restore Project

```powershell
# Navigate to project root
cd "C:\Users\qoose\Desktop\文件資料\客戶分類\S-世運盲恐機Web"

# Restore NuGet packages
dotnet restore

# Build solution
dotnet build

# Run tests (TDD Red-Green-Refactor cycle)
dotnet test
```

---

## 2. Configuration

### appsettings.json

```json
{
  "MesApiSettings": {
    "BaseUrl": "http://192.168.1.100:8080/CimforceTraceMgrDev",
    "DeviceLoginEndpoint": "/api/prtmac/prtmacuserlogin",
    "DataUploadEndpoint": "/api/v1/MesTrace/TraceData/AddData3",
    "BatchValidationEndpoint": "/api/transcode/checkcode",
    "RequestTimeout": "00:00:30",
    "MaxRetryAttempts": 5,
    "RetryDelaySeconds": [ 2, 4, 8, 16, 32 ]
  },
  "EquipmentSettings": {
    "MachineNumber": "S63",
    "IpAddress": "192.168.1.231"
  },
  "Hangfire": {
    "ConnectionString": "Data Source=queue.db",
    "WorkerCount": 1,
    "PollingIntervalSeconds": 15,
    "RetryAttempts": 5
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/mes-api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Hangfire": "Information"
    }
  }
}
```

### Environment Variables (Production)

```powershell
# Set MES API base URL
$env:MesApiSettings__BaseUrl = "http://production-mes-server:8080/CimforceTraceMgrDev"

# Set machine configuration
$env:EquipmentSettings__MachineNumber = "S63"
$env:EquipmentSettings__IpAddress = "192.168.1.231"

# Set database connection
$env:Hangfire__ConnectionString = "Data Source=D:\MesQueue\queue.db"
```

---

## 3. Running the API

### Development Mode

```powershell
# Run with hot reload
cd src/MesTraceApi
dotnet watch run

# API will be available at:
# https://localhost:5001 (HTTPS)
# http://localhost:5000 (HTTP)

# Hangfire dashboard:
# http://localhost:5000/hangfire
```

### Production Mode

```powershell
# Publish for Windows x64
dotnet publish -c Release -r win-x64 --self-contained

# Run published executable
cd bin/Release/net9.0/win-x64/publish
.\MesTraceApi.exe
```

### Windows Service Installation

```powershell
# Install as Windows Service
sc.exe create "MesTraceApi" binPath= "C:\Services\MesTraceApi\MesTraceApi.exe" start= auto
sc.exe description "MesTraceApi" "MES Trace Data Integration Web API"

# Start service
sc.exe start "MesTraceApi"

# Check status
sc.exe query "MesTraceApi"
```

---

## 4. Testing the API

### Contract Tests (TDD Red Phase)

```powershell
# Run contract tests to verify API request/response formats
dotnet test --filter "Category=Contract"

# Expected: Tests FAIL initially (Red phase)
# Output: DeviceLoginContractTests FAILED - endpoint not implemented
```

### Integration Tests (After Green Phase)

```powershell
# Run integration tests with WireMock.Net mock server
dotnet test --filter "Category=Integration"

# Tests cover:
# - Device authentication flow
# - Data upload with retry logic
# - Offline queue operation
# - Batch validation scenarios
```

### Manual Testing with HTTP Files

Open `specs/1-mes-trace-integration/contracts/*.http` files in VS Code with REST Client extension:

1. **device-login.http**: Test authentication
2. **data-upload.http**: Test inspection data upload
3. **batch-validation.http**: Test trace code validation

Click "Send Request" to execute each contract.

---

## 5. Using the API

### Workflow: Device Authentication

```csharp
// 1. Equipment logs in with machine number
POST /api/auth/login
Referrer: 192.168.1.231
Content-Type: application/json

{
  "machineNumber": "S63"
}

// Response:
{
  "success": true,
  "data": {
    "token": "autoprtaed02b44469a0e81a63dec431",
    "expiresAt": "2025-11-12T08:00:00Z"
  },
  "message": "Authentication successful",
  "code": "200"
}
```

### Workflow: Upload Inspection Data

```csharp
// 2. Upload inspection results
POST /api/inspection/upload
accessToken: autoprtaed02b44469a0e81a63dec431
Content-Type: application/json

{
  "rowNo": 1,
  "procName": "ET",
  "devName": "W4-MXDCJ-001",
  "userName": "215123",
  "workClass": "A",
  "traceCode": "09O12380010001",
  "lotNo": "02029156-00800-N",
  "paramData": [
    { "code": "Result", "name": "单板检测结果", "value": "PASS", "unit": "", "desc": "" }
  ],
  "benchmarks": [
    { "code": "Defect_Qty_01", "name": "开路数量", "value": "5", "unit": "", "desc": "" }
  ],
  "otherData": [
    { "code": "CheckTime", "name": "测试时间", "value": "2024-08-29T08:30:00", "unit": "", "desc": "" }
  ]
}

// Response:
{
  "success": true,
  "data": null,
  "message": "上传成功",
  "code": "200"
}
```

### Workflow: Batch Validation

```csharp
// 3. Validate trace codes against work order
POST /api/validation/check
accessToken: autoprtaed02b44469a0e81a63dec431
Content-Type: application/json

{
  "workOrderNumber": "229033-0-1-1",
  "traceCodes": ["0000000001", "0000000002", "0000000003"],
  "machineNumber": "S63"
}

// Response:
{
  "success": true,
  "data": null,
  "message": "验证成功",
  "code": "200"
}
```

---

## 6. Monitoring and Observability

### Hangfire Dashboard

Access the background job dashboard:
- URL: http://localhost:5000/hangfire
- View queued uploads, retry attempts, failed jobs
- Manually requeue failed items

**Dashboard Features**:
- **Enqueued**: Inspection data waiting for upload
- **Processing**: Currently uploading to MES
- **Succeeded**: Successfully uploaded (last 24 hours)
- **Failed**: Requires manual intervention or investigation

### Structured Logs (Serilog)

```powershell
# View real-time logs
Get-Content -Path "logs\mes-api-20251111.log" -Wait

# Query structured logs for specific machine
Select-String -Path "logs\*.log" -Pattern "MachineNumber: S63"

# Find failed uploads
Select-String -Path "logs\*.log" -Pattern "Upload failed"
```

**Log Fields**:
- Timestamp (UTC)
- Level (Information, Warning, Error)
- MachineNumber
- Operation (DeviceLogin, DataUpload, BatchValidation)
- TraceCode/LotNumber
- Duration (ms)
- Success/Failure
- ErrorDetails (if failed)

### Performance Metrics

Key metrics tracked in logs:
- **API response time**: p50, p95, p99 latencies
- **Success rate**: % of successful uploads
- **Retry rate**: % of uploads requiring retry
- **Queue depth**: Number of pending uploads

---

## 7. Offline Operation

### Queue Management

When MES system is unavailable:
1. API queues inspection data to SQLite database (`queue.db`)
2. Hangfire background job retries upload every 2-32 seconds (exponential backoff)
3. After 5 failed attempts, job moves to Failed state
4. Operators can manually requeue via Hangfire dashboard

### Queue Recovery

```powershell
# Check queue database
sqlite3 queue.db "SELECT COUNT(*) FROM QueuedUploads WHERE Status='Pending';"

# View failed uploads
sqlite3 queue.db "SELECT * FROM QueuedUploads WHERE Status='Failed' LIMIT 10;"

# Manual recovery (if needed)
# 1. Open Hangfire dashboard: http://localhost:5000/hangfire
# 2. Navigate to "Failed" tab
# 3. Select failed jobs and click "Requeue"
```

---

## 8. Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| IP mismatch error (code 300) | Equipment IP changed | Update `EquipmentSettings:IpAddress` in config, restart API |
| Token missing (code 401) | Authentication failed | Check machine number, verify MES connectivity |
| Work order not found (code -1) | Invalid trace code/lot number | Verify trace code format, check work order in MES |
| Data upload timeout | MES system slow/unavailable | Check network connectivity, review Hangfire retry queue |
| Queue growing indefinitely | MES system down | Check MES status, manually clear old queue items after recovery |

### Diagnostic Commands

```powershell
# Test MES connectivity
Test-NetConnection -ComputerName 192.168.1.100 -Port 8080

# Check API health
Invoke-WebRequest -Uri "http://localhost:5000/health" -Method GET

# View recent errors
Select-String -Path "logs\*.log" -Pattern "\[Error\]" | Select-Object -Last 20

# Check Hangfire job status
# Visit: http://localhost:5000/hangfire/jobs/failed
```

---

## 9. Deployment Checklist

### Pre-Deployment

- [ ] Run all tests: `dotnet test` (must PASS)
- [ ] Verify MES connectivity from production server
- [ ] Configure `appsettings.Production.json` with correct MES URLs
- [ ] Set machine number and IP address for target equipment
- [ ] Create logs directory with write permissions
- [ ] Create queue database directory with write permissions

### Deployment Steps

1. Publish application: `dotnet publish -c Release`
2. Copy published files to production server
3. Install as Windows Service (see Section 3)
4. Start service and verify logs
5. Test device login endpoint
6. Test data upload with sample inspection data
7. Monitor Hangfire dashboard for 1 hour

### Post-Deployment

- [ ] Verify authentication works from registered IP
- [ ] Upload test inspection record successfully
- [ ] Validate batch codes against test work order
- [ ] Check structured logs are being written
- [ ] Confirm Hangfire queue processing works
- [ ] Test offline operation (disconnect MES, verify queueing)
- [ ] Document production MES endpoints and machine numbers

---

## 10. TDD Red-Green-Refactor Example

### Phase 1: Red (Write Failing Test)

```csharp
[Fact]
public async Task DeviceLogin_WithValidCredentials_ReturnsToken()
{
    // Arrange
    var client = _factory.CreateClient();
    var request = new DeviceLoginRequest { MachineNumber = "S63" };

    // Act
    var response = await client.PostAsJsonAsync("/api/auth/login", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<MesApiResponse<DeviceLoginResponse>>();
    result.Success.Should().BeTrue();
    result.Data.Token.Should().NotBeNullOrEmpty();
}

// Run: dotnet test
// Expected: TEST FAILS - DeviceAuthController not implemented
```

### Phase 2: Green (Make Test Pass)

```csharp
[ApiController]
[Route("api/auth")]
public class DeviceAuthController : ControllerBase
{
    private readonly IMesClientService _mesClient;

    [HttpPost("login")]
    public async Task<ActionResult<MesApiResponse<DeviceLoginResponse>>> Login(
        [FromBody] DeviceLoginRequest request)
    {
        var response = await _mesClient.DeviceLoginAsync(request.MachineNumber);
        return Ok(response);
    }
}

// Run: dotnet test
// Expected: TEST PASSES - minimum implementation
```

### Phase 3: Refactor (Improve Code Quality)

```csharp
// Extract token caching, add validation, improve error handling
[ApiController]
[Route("api/auth")]
public class DeviceAuthController : ControllerBase
{
    private readonly IMesClientService _mesClient;
    private readonly ITokenService _tokenService;
    private readonly IValidator<DeviceLoginRequest> _validator;
    private readonly ILogger<DeviceAuthController> _logger;

    [HttpPost("login")]
    public async Task<ActionResult<MesApiResponse<DeviceLoginResponse>>> Login(
        [FromBody] DeviceLoginRequest request)
    {
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return BadRequest(validationResult.Errors);

        _logger.LogInformation("Device login attempt: {MachineNumber}", request.MachineNumber);

        var response = await _tokenService.GetOrRefreshTokenAsync(request.MachineNumber);

        return Ok(response);
    }
}

// Run: dotnet test
// Expected: TEST STILL PASSES - refactored code maintains behavior
```

---

## Next Steps

1. **Implement User Story 1** (Device Authentication): Follow TDD Red-Green-Refactor cycle
2. **Implement User Story 2** (Inspection Data Upload): Contract tests → Integration tests → Implementation
3. **Implement User Story 3** (Batch Validation): Complete testing before deployment
4. **Performance Testing**: Load test with 100 concurrent uploads
5. **Production Deployment**: Follow deployment checklist
6. **Operator Training**: Hangfire dashboard usage, log monitoring, troubleshooting

---

**For detailed implementation tasks, see**: `specs/1-mes-trace-integration/tasks.md` (generated by `/speckit.tasks` command)
