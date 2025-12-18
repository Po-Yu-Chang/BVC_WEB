# MesMiddleware.Service 發佈腳本
# 產生自包含的單一 exe 檔案，不需要安裝 .NET Runtime

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "..\..\release\Service"
)

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  MesMiddleware.Service 發佈工具" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# 切換到專案目錄
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir "..\src\MesMiddleware.Service"
$outputPath = Join-Path $scriptDir $OutputDir

Write-Host "專案目錄: $projectDir" -ForegroundColor Yellow
Write-Host "輸出目錄: $outputPath" -ForegroundColor Yellow
Write-Host ""

# 清理輸出目錄
if (Test-Path $outputPath) {
    Write-Host "清理舊的發佈檔案..." -ForegroundColor Gray
    Remove-Item -Path $outputPath -Recurse -Force
}

# 發佈
Write-Host "正在發佈 (自包含單一檔案)..." -ForegroundColor Green
dotnet publish $projectDir `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outputPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "發佈失敗!" -ForegroundColor Red
    exit 1
}

# 複製設定檔
Write-Host "複製設定檔..." -ForegroundColor Green
Copy-Item "$projectDir\appsettings.json" $outputPath -Force
Copy-Item "$projectDir\appsettings.Production.json" $outputPath -Force -ErrorAction SilentlyContinue

# 建立安裝腳本
$installScript = @'
@echo off
chcp 65001 >nul
echo ========================================
echo   MesMiddleware.Service 安裝程式
echo ========================================
echo.

:: 檢查管理員權限
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [錯誤] 請以管理員身分執行此腳本！
    echo 右鍵點擊 install-service.bat，選擇「以系統管理員身分執行」
    pause
    exit /b 1
)

:: 安裝服務
echo 正在安裝 Windows 服務...
sc create MesMiddlewareService binPath= "%~dp0MesMiddleware.Service.exe" start= auto DisplayName= "MES Middleware Service"
if %errorLevel% neq 0 (
    echo [錯誤] 服務安裝失敗！
    pause
    exit /b 1
)

:: 設定服務描述
sc description MesMiddlewareService "MES 中轉服務 - 設備資料上傳至 MES 雲端系統"

:: 啟動服務
echo 正在啟動服務...
sc start MesMiddlewareService

echo.
echo [成功] 服務已安裝並啟動！
echo.
echo API 端點: http://localhost:5100
echo Swagger: http://localhost:5100/swagger
echo Hangfire: http://localhost:5100/hangfire
echo.
pause
'@

$uninstallScript = @'
@echo off
chcp 65001 >nul
echo ========================================
echo   MesMiddleware.Service 移除程式
echo ========================================
echo.

:: 檢查管理員權限
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [錯誤] 請以管理員身分執行此腳本！
    pause
    exit /b 1
)

:: 停止服務
echo 正在停止服務...
sc stop MesMiddlewareService >nul 2>&1

:: 等待服務停止
timeout /t 3 /nobreak >nul

:: 移除服務
echo 正在移除服務...
sc delete MesMiddlewareService
if %errorLevel% neq 0 (
    echo [警告] 服務移除可能需要重新開機才能完成
)

echo.
echo [完成] 服務已移除！
pause
'@

$installScript | Out-File -FilePath "$outputPath\install-service.bat" -Encoding UTF8
$uninstallScript | Out-File -FilePath "$outputPath\uninstall-service.bat" -Encoding UTF8

# 建立 README
$readme = @"
# MesMiddleware.Service

## 檔案說明
- MesMiddleware.Service.exe - 主程式 (自包含 .NET Runtime)
- appsettings.json - 設定檔
- install-service.bat - 安裝 Windows 服務 (需管理員權限)
- uninstall-service.bat - 移除 Windows 服務 (需管理員權限)

## 安裝步驟
1. 將整個資料夾複製到目標電腦 (例如: C:\MesMiddleware)
2. 以管理員身分執行 install-service.bat
3. 服務將自動啟動

## 直接執行 (不安裝服務)
直接雙擊 MesMiddleware.Service.exe 即可執行

## API 端點
- 設備資料上傳: POST http://localhost:5100/api/labview/submit
- 狀態查詢: GET http://localhost:5100/api/status
- Swagger 文件: http://localhost:5100/swagger
- Hangfire 面板: http://localhost:5100/hangfire

## 設定檔 (appsettings.json)
修改 WebApi 區段設定 MES 雲端連線資訊
"@

$readme | Out-File -FilePath "$outputPath\README.txt" -Encoding UTF8

# 顯示結果
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  發佈完成！" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "輸出目錄: $outputPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "檔案清單:" -ForegroundColor Yellow
Get-ChildItem $outputPath | Format-Table Name, @{N='Size(MB)';E={[math]::Round($_.Length/1MB,2)}} -AutoSize
Write-Host ""
Write-Host "注意: 此版本為自包含部署，不需要安裝 .NET Runtime" -ForegroundColor Green
