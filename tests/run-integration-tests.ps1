# PowerShell Script to run SignalR integration tests
# This script starts the service, runs tests, then stops the service

Write-Host "=== SignalR Integration Tests Runner ===" -ForegroundColor Cyan

# Step 1: Start the Middleware Service in background
Write-Host "`n[1/4] Starting Middleware Service..." -ForegroundColor Yellow
Push-Location "src\MesMiddleware.Service"

$serviceProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --no-build" -PassThru -NoNewWindow
$servicePid = $serviceProcess.Id

Pop-Location

Write-Host "Service started (PID: $servicePid)" -ForegroundColor Green
Write-Host "Waiting 5 seconds for service to initialize..." -ForegroundColor Gray
Start-Sleep -Seconds 5

# Step 2: Check if service is running
Write-Host "`n[2/4] Checking service health..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5100/health" -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "Service is healthy!" -ForegroundColor Green
    }
} catch {
    Write-Host "Warning: Could not reach health endpoint" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
}

# Step 3: Run integration tests
Write-Host "`n[3/4] Running CommandHub integration tests..." -ForegroundColor Yellow
dotnet test tests\MesMiddleware.Service.Tests\MesMiddleware.Service.Tests.csproj `
    --filter "FullyQualifiedName~CommandHubTests" `
    --logger "console;verbosity=detailed"

$testExitCode = $LASTEXITCODE

# Step 4: Stop the service
Write-Host "`n[4/4] Stopping Middleware Service..." -ForegroundColor Yellow
Stop-Process -Id $servicePid -Force
Write-Host "Service stopped (PID: $servicePid)" -ForegroundColor Green

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
if ($testExitCode -eq 0) {
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host "Some tests failed (Exit Code: $testExitCode)" -ForegroundColor Red
}

exit $testExitCode
