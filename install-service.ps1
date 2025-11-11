# MES Middleware Service - Windows Service Installer
# Requires Administrator privileges

param(
    [string]$ServiceName = "MesMiddlewareService",
    [string]$DisplayName = "MES Middleware Service",
    [string]$Description = "Middleware service for equipment data collection and WebAPI integration",
    [string]$BinPath = "$PSScriptRoot\src\MesMiddleware.Service\bin\Release\net9.0\win-x64\publish\MesMiddleware.Service.exe",
    [string]$StartType = "Automatic"
)

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Error "This script requires Administrator privileges. Please run as Administrator."
    exit 1
}

Write-Host "Installing MES Middleware Service..." -ForegroundColor Cyan

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "Service '$ServiceName' already exists. Stopping and removing..." -ForegroundColor Yellow

    # Stop service if running
    if ($existingService.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force
        Write-Host "Service stopped." -ForegroundColor Green
    }

    # Remove existing service
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
    Write-Host "Existing service removed." -ForegroundColor Green
}

# Verify binary exists
if (-not (Test-Path $BinPath)) {
    Write-Error "Service executable not found at: $BinPath"
    Write-Host "Please build the service first with: dotnet publish -c Release -r win-x64 --self-contained"
    exit 1
}

# Create new service
Write-Host "Creating Windows Service..." -ForegroundColor Cyan
$result = sc.exe create $ServiceName binPath= "`"$BinPath`"" start= $StartType DisplayName= $DisplayName

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service. Error code: $LASTEXITCODE"
    exit 1
}

# Set service description
sc.exe description $ServiceName $Description

# Configure service recovery options (restart on failure)
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

Write-Host "`nService '$ServiceName' installed successfully!" -ForegroundColor Green
Write-Host "`nService Details:" -ForegroundColor Cyan
Write-Host "  Name: $ServiceName"
Write-Host "  Display Name: $DisplayName"
Write-Host "  Binary Path: $BinPath"
Write-Host "  Start Type: $StartType"
Write-Host "  Recovery: Restart on failure (3 attempts)"

# Ask to start service
$startNow = Read-Host "`nDo you want to start the service now? (Y/N)"
if ($startNow -eq 'Y' -or $startNow -eq 'y') {
    Start-Service -Name $ServiceName
    Write-Host "Service started successfully!" -ForegroundColor Green

    # Show service status
    Get-Service -Name $ServiceName | Format-Table -AutoSize
} else {
    Write-Host "Service not started. You can start it manually with: Start-Service -Name $ServiceName" -ForegroundColor Yellow
}

Write-Host "`nInstallation complete!" -ForegroundColor Green
Write-Host "`nUseful commands:" -ForegroundColor Cyan
Write-Host "  Start service:   Start-Service -Name $ServiceName"
Write-Host "  Stop service:    Stop-Service -Name $ServiceName"
Write-Host "  Service status:  Get-Service -Name $ServiceName"
Write-Host "  View logs:       Get-Content logs\middleware-*.log -Tail 50"
Write-Host "  Uninstall:       sc.exe delete $ServiceName"
