# User Story 3 錯誤實作清理總結

## 清理日期
2025-11-20

## 問題說明

先前基於錯誤理解實作了 User Story 3，認為需要 **WPF Monitor → Middleware → 設備 (LabVIEW)** 的雙向命令控制系統 (SignalR WebSocket)。

實際上，根據 MES Cloud API 原始需求文檔（易美科 MES 系統文檔 CIMFORCE-WPT-20240902），系統架構應為：

```
LabVIEW 設備
    ↓ POST /api/prtmac/prtmacuserlogin (登錄獲取 Token)
    ↓ POST /api/v1/MesTrace/TraceData/AddData3 (上傳檢測數據)
MES Cloud (易美科 WPT 系統)

同時:
Middleware Service
    - 接收設備數據: POST /api/inspection/submit
    - 轉發到 MES Cloud
    - 離線隊列 (SQLite + Hangfire)

WPF Monitor
    - HTTP Polling 監控 Middleware 狀態
```

**MES Cloud 是單向接收數據的系統，不存在「下發命令到設備」的功能。**

## 已刪除的檔案 (共 35 個)

### 後端程式碼 (16 個)
- `src/MesMiddleware.Service/Hubs/CommandHub.cs` (155行)
- `src/MesMiddleware.Service/Services/Commands/ICommandService.cs`
- `src/MesMiddleware.Service/Services/Commands/CommandService.cs` (185行)
- `src/MesMiddleware.Service/Validation/EquipmentCommandValidator.cs`
- `src/MesMiddleware.Service/Models/Command.cs`
- `src/MesMiddleware.Service/Models/CommandAck.cs`
- `src/MesMiddleware.Service/Data/Migrations/20251120051403_AddCommandTables.cs`
- `src/MesMiddleware.Service/Data/Migrations/20251120051403_AddCommandTables.Designer.cs`

### 設備端客戶端 (2 個專案)
- `src/MesMiddleware.EquipmentClient/` (整個專案)
  - SignalRCommandClient.cs
  - LabVIEWWrapper.cs
- `src/MesMiddleware.EquipmentClient.TestApp/` (整個專案)
  - Program.cs (測試應用程式)
- `src/MesMiddleware.LabVIEW/` (整個專案)

### Shared Models (3 個)
- `src/MesMiddleware.Shared/Models/EquipmentCommand.cs`
- `src/MesMiddleware.Shared/Models/CommandAcknowledgment.cs`
- `src/MesMiddleware.Shared/Models/CommandHistoryDto.cs`

### WPF Monitor (2 個)
- `src/MesMiddleware.Monitor/ViewModels/CommandViewModel.cs` (240行)
- `src/MesMiddleware.Monitor/ViewModels/CommandHistoryViewModel.cs`

### 測試檔案 (5 個)
- `tests/MesMiddleware.Service.Tests/Integration/CommandHubTests.cs` (283行, 8測試)
- `tests/MesMiddleware.Service.Tests/Integration/CommandHubTests_WithWebAppFactory.cs` (118行, 2測試)
- `tests/MesMiddleware.Service.Tests/Unit/CommandValidatorTests.cs` (172行, 8測試)
- `tests/MesMiddleware.Service.Tests/Unit/CommandServiceTests.cs` (220行, 10測試)
- `tests/MesMiddleware.Monitor.Tests/Unit/CommandViewModelTests.cs` (258行, 9測試)

### 文檔檔案 (10 個)
- `QUICKSTART_SIGNALR.md`
- `EQUIPMENT_SIGNALR_INTEGRATION.md`
- `DEPLOYMENT_SIGNALR.md`
- `DELIVERY_PACKAGE.md`
- `US3_DELIVERY_SUMMARY.md`
- `US3_TDD_IMPLEMENTATION_GUIDE.md`
- `US3_TDD_RED_PHASE_COMPLETE.md`
- `USER_STORY_3_ROADMAP.md`
- `TDD_COVERAGE_ANALYSIS.md`
- `MES_REQUIREMENTS_EXTRACTED.txt`

## 已還原的檔案 (7 個)

### Program.cs
- 移除 `using MesMiddleware.Service.Hubs`
- 移除 `using MesMiddleware.Service.Services.Commands`
- 移除 `builder.Services.AddSignalR()`
- 移除 `builder.Services.AddScoped<IValidator<EquipmentCommand>, EquipmentCommandValidator>()`
- 移除 `builder.Services.AddScoped<ICommandService, CommandService>()`
- 移除 `app.MapHub<CommandHub>("/commandHub")`

### MiddlewareDbContext.cs
- 移除 `public DbSet<Command> Commands`
- 移除 `public DbSet<CommandAck> CommandAcknowledgments`
- 移除 Command/CommandAck 實體配置

### IMesWebApiClient.cs
- 移除 `Task<bool> SendCommandAcknowledgmentAsync(...)`

### MesWebApiClient.cs
- 移除 `SendCommandAcknowledgmentAsync()` 方法實作 (40行)

### IMiddlewareApiClient.cs
- 移除 `Task<List<CommandHistoryDto>> GetCommandHistoryAsync(...)`
- 移除 `Task<bool> SendCommandAsync(...)`
- 移除 `Task<bool> CancelCommandAsync(...)`

### MiddlewareApiClient.cs
- 移除 `GetCommandHistoryAsync()` 方法 (TODO placeholder, 15行)
- 移除 `SendCommandAsync()` 方法 (TODO placeholder, 18行)
- 移除 `CancelCommandAsync()` 方法 (TODO placeholder, 18行)

### MiddlewareDbContextModelSnapshot.cs
- 自動更新（移除 Commands/CommandAcknowledgments 表）

## 統計數據

### 刪除的程式碼
- 總檔案數: **35 個檔案**
- 總程式碼行數: **約 2000+ 行**
- 測試數量: **37 個測試** (全部刪除)
  - CommandHubTests: 8 個
  - CommandHubTests_WithWebAppFactory: 2 個
  - CommandValidatorTests: 8 個
  - CommandServiceTests: 10 個
  - CommandViewModelTests: 9 個

### 修改的程式碼
- Program.cs: 刪除 10 行
- MiddlewareDbContext.cs: 刪除 46 行
- IMesWebApiClient.cs: 刪除 7 行
- MesWebApiClient.cs: 刪除 40 行
- IMiddlewareApiClient.cs: 刪除 24 行
- MiddlewareApiClient.cs: 刪除 51 行

## 編譯驗證

✅ **編譯成功 (0 錯誤, 0 警告)**:
- MesMiddleware.Shared
- MesMiddleware.Service
- MesMiddleware.Monitor

## 正確的系統實作狀態

### ✅ 已正確實作 (User Story 1 & 2)

**User Story 1: 設備數據收集與上傳**
- LabVIEW → Middleware (POST /api/inspection/submit)
- Middleware → MES Cloud (使用 Token 認證)
- 三層隊列架構 (Channel + SQLite + Hangfire)
- 離線重試機制

**User Story 2: 實時監控儀表板**
- WPF Monitor 透過 HTTP Polling 監控 Middleware
- 顯示連線狀態、上傳統計、歷史記錄
- 支援多語言 (zh-TW, zh-CN, en)

### ❌ 不需實作 (User Story 3)

根據 MES Cloud API 文檔，**不存在雙向命令控制需求**。

MES Cloud 提供的 API:
1. `/api/prtmac/prtmacuserlogin` - 設備登錄
2. `/api/v1/MesTrace/TraceData/AddData3` - 數據上傳
3. `/api/transcode/checkcode` - 追溯碼校驗

**所有 API 都是設備→MES Cloud 單向上傳，沒有 MES Cloud→設備的命令下發。**

## 後續工作

### 需要更新的文檔
1. ✅ CLEANUP_SUMMARY.md (本文件)
2. ⏳ SRS.md - 移除 User Story 3 相關章節
3. ⏳ README.md - 更新系統架構說明
4. ⏳ CLAUDE.md - 移除 User Story 3 提及
5. ⏳ 新增 MES_CLOUD_API_SPEC.md - 根據 PDF 文檔建立正確的 MES Cloud API 規格

### 資料庫清理
由於已刪除 AddCommandTables 遷移，現有資料庫不受影響。
若需重新建立乾淨的資料庫:
```powershell
rm Data/queue.db
dotnet ef database update
```

## 結論

所有錯誤的 User Story 3 實作已完全清理，系統恢復為正確的 **單向數據上傳架構**：

```
LabVIEW → Middleware → MES Cloud ✅
WPF Monitor → Middleware (查詢狀態) ✅
```

編譯測試通過，可繼續進行正確的功能開發。
