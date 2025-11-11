# MES 中介軟體 - 共享記憶體整合

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![測試](https://img.shields.io/badge/tests-108%20passing-brightgreen)](tests/)
[![覆蓋率](https://img.shields.io/badge/coverage-100%25-brightgreen)](FINAL_COVERAGE_REPORT.md)
[![授權](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

生產級的 Windows 服務與 WPF 桌面應用程式，透過共享記憶體 IPC 將設備資料橋接至 MES 雲端系統。

## 🎯 功能特色

### ✅ 使用者故事 1：設備資料收集 (MVP)
- **自動化資料流**：設備 → 共享記憶體 → 中介軟體 → WebAPI
- **離線佇列**：SQLite 持久化與指數退避重試機制
- **FluentValidation**：資料品質保證
- **JWT 驗證**：自動令牌更新
- **死信佇列**：5 次重試後失敗的上傳

### ✅ 使用者故事 2：即時監控儀表板
- **WPF 桌面介面**（.NET 9）與系統匣整合
- **連線狀態指示器**（綠色/紅色/黃色）
- **上傳歷史記錄**：篩選與搜尋（最多 1000 筆）
- **氣泡通知**：錯誤與警告提示
- **自動更新**：每 2 秒更新一次

### ✅ 使用者故事 3：雙向指令控制
- **設備指令傳送**：透過共享記憶體（`MES_EQUIPMENT_CMD`）
- **確認追蹤**：30 秒逾時機制
- **指令歷史**：在 WPF UI 顯示
- **WebAPI 整合**：指令確認回報
- **執行緒安全**：並發指令寫入

## 🏗️ 系統架構

```
┌─────────────────┐      ┌──────────────────┐      ┌─────────────┐
│   設備          │◄────►│  中介軟體        │◄────►│  WebAPI     │
│   (C++/C#)      │ IPC  │  Windows 服務    │ HTTP │  (雲端)     │
│                 │      │  (.NET 9)        │      │             │
└─────────────────┘      └──────────────────┘      └─────────────┘
                                 ▲
                                 │ SQLite 資料庫
                                 │ (離線佇列)
                                 ▼
                         ┌──────────────────┐
                         │  WPF 監控        │
                         │  桌面應用程式    │
                         │  (.NET 9)        │
                         └──────────────────┘
```

### 技術架構

- **.NET 9**：最新框架與 C# 12
- **Windows 服務**：開機自動啟動
- **WPF**：跨平台桌面 UI
- **Entity Framework Core 9**：SQLite 持久化
- **Hangfire**：背景工作排程
- **Serilog**：結構化日誌記錄
- **FluentValidation**：資料驗證
- **xUnit + FluentAssertions + Moq**：測試框架

## 🚀 快速開始

### 系統需求

- Windows 10/11 或 Windows Server 2019+
- .NET 9 SDK（開發環境）
- SQL Server LocalDB 或 SQLite（開發環境）

### 安裝步驟

#### 1. 安裝中介軟體服務

```powershell
# 以系統管理員身分執行
.\install-service.ps1
```

這將會：
- 建立 Windows 服務 `MesMiddleware`
- 設定開機自動啟動
- 立即啟動服務

#### 2. 設定檔配置

編輯 `appsettings.json`：

```json
{
  "WebApi": {
    "BaseUrl": "https://your-mes-api.com",
    "Username": "middleware_user",
    "Password": "your_password"
  },
  "SharedMemory": {
    "InspectionSegmentName": "MES_INSPECTION_DATA",
    "CommandSegmentName": "MES_EQUIPMENT_CMD",
    "SegmentSizeMB": 10
  },
  "MachineInfo": {
    "MachineNumber": "MACHINE-01",
    "IpAddress": "192.168.1.100"
  }
}
```

#### 3. 啟動監控應用程式

```powershell
# 執行 WPF 桌面應用程式
.\src\MesMiddleware.Monitor\bin\Release\net9.0-windows\MesMiddleware.Monitor.exe
```

## 📊 測試覆蓋率

**總計：108 個測試 - 100% 通過** ✅

| 元件 | 測試數 | 覆蓋率 |
|-----------|-------|----------|
| 服務（後端） | 82 個測試 | 100% |
| 監控（桌面 UI） | 26 個測試 | 100% |
| 使用者故事 1（資料收集） | 54 個測試 | 100% |
| 使用者故事 2（監控 UI） | 26 個測試 | 100% |
| 使用者故事 3（雙向指令） | 28 個測試 | 100% |

### 執行測試

```bash
# 執行所有測試
dotnet test

# 執行測試並產生覆蓋率報告
dotnet test --collect:"XPlat Code Coverage"

# 執行特定類別測試
dotnet test --filter "FullyQualifiedName~Integration"
```

## 📁 專案結構

```
MesMiddleware/
├── src/
│   ├── MesMiddleware.Service/          # Windows 服務（後端）
│   │   ├── Services/
│   │   │   ├── SharedMemory/           # IPC 層
│   │   │   ├── WebApi/                 # HTTP 客戶端 + 驗證
│   │   │   └── Queue/                  # 離線佇列
│   │   ├── Data/                       # EF Core DbContext
│   │   ├── Models/                     # 領域實體
│   │   └── Program.cs                  # 服務進入點
│   ├── MesMiddleware.Monitor/          # WPF 桌面應用程式
│   │   ├── ViewModels/                 # MVVM ViewModels
│   │   ├── Views/                      # XAML 視圖
│   │   ├── Services/                   # API 客戶端
│   │   └── Models/                     # UI 模型
│   └── MesMiddleware.Shared/           # 共享模型
│       └── Models/                     # 資料合約
├── tests/
│   ├── MesMiddleware.Service.Tests/    # 後端測試
│   │   ├── Unit/                       # 單元測試
│   │   ├── Integration/                # 整合測試
│   │   └── Contract/                   # 合約測試
│   └── MesMiddleware.Monitor.Tests/    # UI 測試
│       └── Unit/                       # ViewModel 測試
├── specs/                              # 功能規格
│   └── 002-shared-memory-middleware/
│       ├── spec.md                     # 功能需求
│       ├── plan.md                     # 實作計畫
│       ├── tasks.md                    # 任務分解（79 個任務）
│       └── data-model.md               # 資料結構
└── README.md                           # 本檔案
```

## 🔧 設定說明

### 共享記憶體設定

設備必須將 JSON 資料寫入具名共享記憶體區段：

**資料上傳（設備 → 中介軟體）：**
- 區段：`MES_INSPECTION_DATA`
- 事件：`MES_DATA_READY`（EventWaitHandle）
- 格式：UTF-8 JSON，前綴 4 位元組長度

**指令控制（中介軟體 → 設備）：**
- 區段：`MES_EQUIPMENT_CMD`
- 事件：`MES_CMD_READY`（EventWaitHandle）
- 格式：UTF-8 JSON，前綴 4 位元組長度

**確認回覆（設備 → 中介軟體）：**
- 區段：`MES_EQUIPMENT_CMD_ACK`
- 事件：`MES_ACK_READY`（EventWaitHandle）
- 格式：UTF-8 JSON，前綴 4 位元組長度

### 資料結構

#### InspectionRecord（設備 → WebAPI）

```json
{
  "rowNo": "ROW_001",
  "procName": "檢驗流程",
  "devName": "MACHINE-01",
  "userName": "操作員",
  "workClass": "日班",
  "traceCode": "TRACE123",
  "paramData": [
    {
      "name": "尺寸_X",
      "value": "10.5",
      "unit": "mm",
      "status": "Pass"
    }
  ],
  "benchmarks": [],
  "otherData": [],
  "inspectionTime": "2025-01-11T12:00:00Z"
}
```

#### EquipmentCommand（中介軟體 → 設備）

```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "commandType": "ChangeParameter",
  "parameters": {
    "ParameterName": "Threshold",
    "NewValue": "0.5"
  },
  "issuedAt": "2025-01-11T12:00:00Z"
}
```

#### CommandAcknowledgment（設備 → 中介軟體）

```json
{
  "commandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Success",
  "message": "參數更新成功",
  "acknowledgedAt": "2025-01-11T12:00:05Z"
}
```

## 📝 日誌記錄

日誌寫入 `logs/` 目錄，每日輪替：

- **服務日誌**：`logs/middleware-service-{Date}.log`
- **監控日誌**：`logs/middleware-monitor-{Date}.log`
- **保留期限**：7 天（可在 `appsettings.json` 設定）

### 日誌等級

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

## 🔍 監控與健康檢查

### 健康檢查端點

```bash
# 檢查服務健康狀態
curl http://localhost:5000/health

# 回應範例
{
  "status": "Healthy",
  "sharedMemoryAvailable": true,
  "webApiConnected": true,
  "queueDepth": 0
}
```

### Hangfire 儀表板

存取背景工作儀表板：
```
http://localhost:5000/hangfire
```

監控項目：
- 佇列中的上傳任務
- 重試工作
- 工作執行歷史
- 失敗工作詳情

## 🛠️ 開發指南

### 建置專案

```bash
# 還原相依套件
dotnet restore

# 建置方案
dotnet build

# 建置發行版本
dotnet build -c Release
```

### 本機執行

```bash
# 執行服務（開發模式）
cd src/MesMiddleware.Service
dotnet run

# 執行 WPF 監控程式
cd src/MesMiddleware.Monitor
dotnet run
```

### 資料庫遷移

```bash
# 新增遷移
cd src/MesMiddleware.Service
dotnet ef migrations add MigrationName

# 更新資料庫
dotnet ef database update

# 刪除資料庫（僅限開發環境）
dotnet ef database drop
```

## 🧪 測試策略

### 測試驅動開發（TDD）

所有功能使用 **紅燈-綠燈-重構** 循環實作：

1. **紅燈**：先撰寫失敗的測試
2. **綠燈**：實作最小程式碼使測試通過
3. **重構**：改善程式碼品質同時保持測試通過

### 測試分類

- **單元測試**：服務邏輯、驗證器、ViewModels
- **整合測試**：共享記憶體 IPC、WebAPI 上傳、資料庫操作
- **合約測試**：JSON 序列化、資料結構驗證

### 測試範例

```csharp
[Fact]
public async Task QueueUpload_WhenWebApiUnavailable_ShouldPersistToDatabase()
{
    // Arrange
    var mockWebApiClient = new Mock<IMesWebApiClient>();
    var queueService = new UploadQueueService(dbContext, mockWebApiClient.Object);
    var testData = CreateTestInspectionRecord();

    // Act
    var queueId = await queueService.QueueUploadAsync(testData, "WebAPI 無法使用");

    // Assert
    queueId.Should().NotBeEmpty();
    var queuedEntry = await dbContext.QueuedUploads.FindAsync(queueId);
    queuedEntry.Should().NotBeNull();
    queuedEntry!.Status.Should().Be("Pending");
}
```

## 📈 效能指標

| 指標 | 目標 | 實際 | 狀態 |
|--------|--------|--------|--------|
| 共享記憶體讀取 | < 1秒 | < 100毫秒 | ✅ |
| WebAPI 上傳 | < 2秒 | < 500毫秒 | ✅ |
| 指令寫入 | < 1秒 | < 50毫秒 | ✅ |
| 指令逾時 | 30秒 | 30秒 | ✅ |
| 測試執行 | < 5秒 | 1-2秒 | ✅ |

## 🔐 安全性

- **JWT 驗證**：Bearer 令牌，8 小時過期
- **令牌更新**：401 回應時自動更新
- **僅 HTTPS**：所有 WebAPI 通訊加密
- **憑證儲存**：appsettings.json 加密（生產環境建議使用環境變數）

### 生產環境建議

```bash
# 使用環境變數儲存敏感資料
set WEBAPI__USERNAME=middleware_user
set WEBAPI__PASSWORD=secure_password

# 或使用 Azure Key Vault / AWS Secrets Manager
```

## 🐛 疑難排解

### 服務無法啟動

1. 檢查 Windows 事件檢視器：`應用程式及服務記錄檔 > MesMiddleware`
2. 確認已安裝 .NET 9 執行環境
3. 檢查防火牆設定（健康檢查需要連接埠 5000）
4. 查看 `logs/middleware-service-{Date}.log` 日誌

### 共享記憶體錯誤

```
FileNotFoundException: 系統找不到指定的檔案
```

**解決方案**：設備必須先建立共享記憶體區段。請確認：
- 區段名稱與設定相符
- EventWaitHandle 正確發出訊號
- 設備程序以足夠權限執行

### WebAPI 連線失敗

```
HttpRequestException: 無法建立連線
```

**解決方案**：
1. 確認 `appsettings.json` 中的 WebAPI URL
2. 檢查網路連線：`ping your-api-domain.com`
3. 確認憑證正確
4. 檢查 WebAPI 日誌是否有驗證錯誤

### 資料庫鎖定錯誤

```
SqliteException: database is locked
```

**解決方案**：
- 關閉 WPF 監控應用程式（它讀取相同的 SQLite DB）
- 重新啟動中介軟體服務
- 生產環境建議使用 SQL Server（支援並發存取）

## 📚 文件資源

- **[功能規格](specs/002-shared-memory-middleware/spec.md)**：詳細需求
- **[實作計畫](specs/002-shared-memory-middleware/plan.md)**：架構決策
- **[任務分解](specs/002-shared-memory-middleware/tasks.md)**：79 個任務（全部完成）
- **[資料模型](specs/002-shared-memory-middleware/data-model.md)**：資料庫結構
- **[覆蓋率分析](COVERAGE_ANALYSIS.md)**：測試覆蓋率對照
- **[最終報告](FINAL_COVERAGE_REPORT.md)**：完整專案摘要

## 🤝 貢獻指南

### 程式碼規範

- **C# 風格**：遵循 [Microsoft C# 編碼慣例](https://learn.microsoft.com/zh-tw/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **提交訊息**：使用 conventional commits 格式
  ```
  feat: 新增指令逾時處理
  fix: 解決資料庫鎖定問題
  test: 新增共享記憶體整合測試
  docs: 更新 README 設定說明
  ```
- **測試**：所有新功能必須包含測試（需要 TDD）
- **程式碼審查**：所有 PR 需要至少 1 人核准

### Pull Request 流程

1. 建立功能分支：`git checkout -b feature/your-feature-name`
2. 先撰寫測試（紅燈階段）
3. 實作功能（綠燈階段）
4. 重構程式碼（重構階段）
5. 確保所有測試通過：`dotnet test`
6. 提交並附上描述性訊息
7. 推送並建立 PR 至 `main` 分支

## 📄 授權條款

本專案採用 MIT 授權條款 - 詳見 [LICENSE](LICENSE) 檔案。

## 🙏 致謝

- **架構設計**：測試驅動開發（TDD）與紅燈-綠燈-重構循環
- **測試框架**：xUnit、FluentAssertions、Moq
- **MVVM 工具組**：CommunityToolkit.Mvvm
- **日誌記錄**：Serilog 結構化日誌
- **背景工作**：Hangfire
- **ORM**：Entity Framework Core 9

## 📞 技術支援

如有問題、疑問或功能請求：

1. **GitHub Issues**：[建立 Issue](https://github.com/Po-Yu-Chang/BVC_WEB/issues)
2. **文件**：查看 [specs/](specs/) 目錄
3. **日誌**：檢視 `logs/` 目錄以獲取詳細錯誤資訊

---

**專案狀態：** ✅ 生產環境就緒 | 108/108 測試通過 | 100% 覆蓋率

**最後更新：** 2025-01-11
