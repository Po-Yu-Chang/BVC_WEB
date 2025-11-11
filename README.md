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
- **多國語言支援**：繁體中文 / 簡體中文 / English（即時切換）
- **連線狀態指示器**（綠色/紅色/黃色）
- **上傳歷史記錄**：篩選與搜尋（最多 1000 筆）
- **氣泡通知**：錯誤與警告提示
- **自動更新**：每 2 秒更新一次
- **Cork Board UI**：便利貼風格的現代化介面

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

## 🌐 多國語言支援

監控應用程式支援三種語言，可即時切換：

### 支援的語言

- **繁體中文（zh-TW）**：預設語言
- **简体中文（zh-CN）**：簡體中文
- **English（en）**：英文

### 切換語言

1. 啟動監控應用程式
2. 點擊右上角的語言按鈕：
   - **English** - 切換到英文
   - **简体中文** - 切換到簡體中文
   - **繁體中文** - 切換到繁體中文

所有 UI 元素（視窗標題、Tab、卡片、按鈕、表格欄位）會立即更新。

### 本地化內容

- 視窗標題和 Tab 標題
- 所有狀態卡片標籤（23 個元件）
- 按鈕和選單文字
- 系統匣選單
- 表格欄位標題

### 技術實作

- **資源檔案**：.resx 檔案，支援標準 .NET 本地化
- **即時切換**：使用 ILocalizationService 和 MVVM 資料綁定
- **可擴展**：輕鬆新增其他語言

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

## 🔗 異質系統整合指南（C# ↔ LabVIEW）

### 共享記憶體協議規格

本系統使用 Windows **MemoryMappedFile** 和 **EventWaitHandle** 實現跨程序通訊。

#### 記憶體布局

```
┌──────────────────────────────────────────────────┐
│ Byte 0-3:   資料長度 (int32, little-endian)      │
├──────────────────────────────────────────────────┤
│ Byte 4-N:   UTF-8 JSON 字串                      │
└──────────────────────────────────────────────────┘
```

#### 共享區段規格

| 區段名稱 | 大小 | 用途 | 寫入方 | 讀取方 |
|----------|------|------|--------|--------|
| `MES_INSPECTION_DATA` | 10 MB | 檢驗資料 | 設備 | 中介軟體 |
| `MES_EQUIPMENT_CMD` | 10 MB | 設備指令 | 中介軟體 | 設備 |
| `MES_EQUIPMENT_CMD_ACK` | 10 MB | 指令確認 | 設備 | 中介軟體 |

#### 事件信號規格

| 事件名稱 | 用途 | 觸發時機 |
|----------|------|----------|
| `MES_DATA_READY` | 通知中介軟體資料已準備 | 設備寫入資料後 |
| `MES_CMD_READY` | 通知設備指令已準備 | 中介軟體寫入指令後 |
| `MES_ACK_READY` | 通知中介軟體確認已準備 | 設備寫入確認後 |

### C# 實作範例

#### 1. 寫入資料到共享記憶體（設備端）

```csharp
using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using System.Threading;

public class SharedMemoryWriter
{
    private const string SegmentName = "MES_INSPECTION_DATA";
    private const string EventName = "MES_DATA_READY";
    private const long SegmentSize = 10 * 1024 * 1024; // 10MB

    public void WriteInspectionData(object data)
    {
        // 1. 序列化為 JSON
        string json = JsonSerializer.Serialize(data);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        Console.WriteLine($"JSON size: {jsonBytes.Length} bytes");

        // 2. 建立或開啟共享記憶體
        using (var mmf = MemoryMappedFile.CreateOrOpen(SegmentName, SegmentSize))
        using (var accessor = mmf.CreateViewAccessor())
        {
            // 3. 寫入長度（4 bytes, little-endian）
            accessor.Write(0, jsonBytes.Length);

            // 4. 寫入 JSON 資料
            accessor.WriteArray(4, jsonBytes, 0, jsonBytes.Length);

            Console.WriteLine($"Written {jsonBytes.Length} bytes to shared memory");
        }

        // 5. 發送信號通知中介軟體
        using (var eventHandle = new EventWaitHandle(false,
                   EventResetMode.AutoReset, EventName))
        {
            eventHandle.Set();
            Console.WriteLine("Signal sent to middleware");
        }
    }
}

// 使用範例
var writer = new SharedMemoryWriter();
var inspectionData = new
{
    rowNo = "ROW_001",
    procName = "檢驗流程",
    devName = "MACHINE-01",
    traceCode = "TRACE123",
    inspectionTime = DateTime.UtcNow
};
writer.WriteInspectionData(inspectionData);
```

#### 2. 從共享記憶體讀取資料（設備端接收指令）

```csharp
using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json;
using System.Threading;

public class SharedMemoryReader
{
    private const string SegmentName = "MES_EQUIPMENT_CMD";
    private const string EventName = "MES_CMD_READY";

    public string WaitForCommand(int timeoutSeconds = 30)
    {
        // 1. 等待信號
        using (var eventHandle = new EventWaitHandle(false,
                   EventResetMode.AutoReset, EventName))
        {
            bool signaled = eventHandle.WaitOne(timeoutSeconds * 1000);
            if (!signaled)
            {
                Console.WriteLine("Timeout waiting for command");
                return null;
            }
            Console.WriteLine("Command signal received");
        }

        // 2. 開啟共享記憶體
        using (var mmf = MemoryMappedFile.OpenExisting(SegmentName))
        using (var accessor = mmf.CreateViewAccessor())
        {
            // 3. 讀取長度
            int length = accessor.ReadInt32(0);
            Console.WriteLine($"Command length: {length} bytes");

            // 4. 讀取 JSON 資料
            byte[] buffer = new byte[length];
            accessor.ReadArray(4, buffer, 0, length);

            // 5. 解碼為字串
            string json = Encoding.UTF8.GetString(buffer);
            Console.WriteLine($"Received command: {json}");

            return json;
        }
    }
}

// 使用範例
var reader = new SharedMemoryReader();
while (true)
{
    string commandJson = reader.WaitForCommand();
    if (commandJson != null)
    {
        // 處理指令
        var command = JsonSerializer.Deserialize<EquipmentCommand>(commandJson);
        Console.WriteLine($"Processing command: {command.CommandType}");
    }
}
```

### LabVIEW 實作範例

LabVIEW 使用 **Call Library Function Node** 調用 Windows API。

#### 所需的 Windows API 函數

在 LabVIEW 中需要調用以下 DLL：
- `kernel32.dll` - CreateFileMapping, OpenFileMapping, MapViewOfFile, UnmapViewOfFile, CloseHandle
- `kernel32.dll` - CreateEvent, OpenEvent, SetEvent, WaitForSingleObject

#### LabVIEW Block Diagram 結構

```
┌─────────────────────────────────────────────────────┐
│                寫入資料到共享記憶體                  │
├─────────────────────────────────────────────────────┤
│ 1. [JSON Library] 將資料序列化為 JSON 字串         │
│ 2. [String to Byte Array] 轉換為 UTF-8 bytes       │
│ 3. [Call Library: CreateFileMapping]               │
│    - Name: "MES_INSPECTION_DATA"                   │
│    - Size: 10485760 (10MB)                         │
│ 4. [Call Library: MapViewOfFile]                   │
│ 5. [Memory Write] 寫入長度（4 bytes）              │
│ 6. [Memory Write] 寫入 JSON bytes                  │
│ 7. [Call Library: UnmapViewOfFile]                 │
│ 8. [Call Library: CloseHandle]                     │
│ 9. [Call Library: CreateEvent / SetEvent]          │
│    - Name: "MES_DATA_READY"                        │
└─────────────────────────────────────────────────────┘
```

#### LabVIEW Call Library Function 配置

**1. CreateFileMapping** (kernel32.dll)

```
Function Name: CreateFileMappingA
Return Type: Numeric > U32 (Handle)

Parameters:
  [IN] hFile:           Numeric > I32         = -1 (INVALID_HANDLE_VALUE)
  [IN] lpAttributes:    Numeric > U32         = 0
  [IN] flProtect:       Numeric > U32         = 4 (PAGE_READWRITE)
  [IN] dwMaxSizeHigh:   Numeric > U32         = 0
  [IN] dwMaxSizeLow:    Numeric > U32         = 10485760
  [IN] lpName:          String > C String     = "MES_INSPECTION_DATA"
```

**2. MapViewOfFile** (kernel32.dll)

```
Function Name: MapViewOfFile
Return Type: Numeric > U32 (Pointer)

Parameters:
  [IN] hFileMappingObject: Numeric > U32      = (from CreateFileMapping)
  [IN] dwDesiredAccess:    Numeric > U32      = 2 (FILE_MAP_WRITE)
  [IN] dwFileOffsetHigh:   Numeric > U32      = 0
  [IN] dwFileOffsetLow:    Numeric > U32      = 0
  [IN] dwNumberOfBytes:    Numeric > U32      = 10485760
```

**3. MoveBlock** (kernel32.dll)

```
Function Name: RtlMoveMemory
Return Type: (none)

Parameters:
  [IN] Destination:  Numeric > U32            = (pointer from MapViewOfFile)
  [IN] Source:       Array > 1D Array of U8   = (your data bytes)
  [IN] Length:       Numeric > U32            = (byte count)
```

**4. CreateEvent / SetEvent** (kernel32.dll)

```
Function Name: CreateEventA
Return Type: Numeric > U32 (Handle)

Parameters:
  [IN] lpEventAttributes: Numeric > U32       = 0
  [IN] bManualReset:      Numeric > U32       = 0 (Auto-reset)
  [IN] bInitialState:     Numeric > U32       = 0 (Non-signaled)
  [IN] lpName:            String > C String   = "MES_DATA_READY"

---

Function Name: SetEvent
Return Type: Numeric > U32 (BOOL)

Parameters:
  [IN] hEvent:  Numeric > U32                 = (from CreateEvent)
```

#### LabVIEW 完整寫入流程 VI

```
┌──────────────────────────────────────────────────┐
│           WriteInspectionData.vi                 │
├──────────────────────────────────────────────────┤
│ Input: Cluster (Inspection Data)                │
│ Output: Boolean (Success)                       │
│                                                  │
│ 1. Flatten to JSON (使用 JSONtext 套件)         │
│ 2. String to Byte Array (UTF-8)                 │
│ 3. Prepend Array: 插入 4-byte length prefix     │
│    - Use "Type Cast" U32 → 4 bytes U8[]         │
│ 4. CreateFileMappingA                           │
│    - 檢查 Handle != 0                           │
│ 5. MapViewOfFile                                │
│    - 檢查 Pointer != 0                          │
│ 6. RtlMoveMemory (Write data to mapped memory) │
│ 7. UnmapViewOfFile                              │
│ 8. CloseHandle (file mapping)                   │
│ 9. CreateEventA("MES_DATA_READY")               │
│ 10. SetEvent                                    │
│ 11. CloseHandle (event)                         │
└──────────────────────────────────────────────────┘
```

#### LabVIEW 讀取共享記憶體（接收指令）

```
┌──────────────────────────────────────────────────┐
│            WaitForCommand.vi                     │
├──────────────────────────────────────────────────┤
│ Input: Timeout (seconds)                         │
│ Output: String (JSON command)                    │
│                                                  │
│ 1. OpenEventA("MES_CMD_READY")                   │
│ 2. WaitForSingleObject                          │
│    - Timeout: (input) * 1000 ms                 │
│    - Return: 0 = signaled, 258 = timeout        │
│ 3. If signaled:                                 │
│    a. OpenFileMappingA("MES_EQUIPMENT_CMD")     │
│    b. MapViewOfFile (read mode)                 │
│    c. MoveBlock (copy 4 bytes → length)         │
│    d. MoveBlock (copy N bytes → JSON data)      │
│    e. Byte Array to String (UTF-8)              │
│    f. UnmapViewOfFile                           │
│    g. CloseHandle                               │
│ 4. CloseHandle (event)                          │
└──────────────────────────────────────────────────┘
```

### 記憶體安全注意事項

#### 1. 執行緒安全

- **C#**: 使用 `SemaphoreSlim` 或 `lock` 保護寫入操作
- **LabVIEW**: 使用 "Single-Threaded" execution 或 Semaphore VI

#### 2. 錯誤處理

```csharp
// C# 錯誤處理範例
try
{
    using var mmf = MemoryMappedFile.OpenExisting(SegmentName);
}
catch (FileNotFoundException)
{
    Console.Error.WriteLine("共享記憶體區段不存在，請先啟動中介軟體");
    return;
}
```

```labview
// LabVIEW 錯誤處理
If (Handle == 0)  // CreateFileMapping failed
    → Get Last Error (kernel32.dll:GetLastError)
    → Display Error Message
    → Exit with Error
```

#### 3. 資源清理

**重要**: 必須確保釋放所有資源，避免記憶體洩漏

- **C#**: 使用 `using` 語句自動釋放
- **LabVIEW**: 在 "While Loop" 外部放置 CloseHandle，或使用 "Error In/Out" 確保執行

#### 4. 資料驗證

```csharp
// 讀取前驗證資料長度
int length = accessor.ReadInt32(0);
if (length <= 0 || length > SegmentSize - 4)
{
    throw new InvalidDataException($"Invalid data length: {length}");
}
```

### 測試與除錯

#### 測試工具

1. **記憶體檢視器**: 使用 [WinObj](https://learn.microsoft.com/en-us/sysinternals/downloads/winobj) 查看共享記憶體區段
2. **事件檢視器**: 查看 EventWaitHandle 狀態
3. **日誌追蹤**: 在兩端都輸出詳細日誌

#### 常見問題

**問題 1**: `FileNotFoundException` - 找不到共享記憶體

**解決方案**:
- 確認另一端已經先創建區段（使用 `CreateOrOpen` 而非 `OpenExisting`）
- 檢查區段名稱拼寫是否完全一致（區分大小寫）

**問題 2**: 讀取到亂碼或空資料

**解決方案**:
- 確認使用 UTF-8 編碼（`Encoding.UTF8`）
- 檢查長度前綴是否正確（little-endian int32）
- 使用十六進位檢視器檢查記憶體內容

**問題 3**: 事件信號未觸發

**解決方案**:
- 確認事件名稱正確
- 使用 `AutoReset` 模式（每次信號後自動重置）
- 檢查是否有多個程序同時等待（只有一個會被喚醒）

### 效能最佳化

#### 1. 減少記憶體拷貝

```csharp
// 不佳: 多次拷貝
var json = JsonSerializer.Serialize(data);
var bytes = Encoding.UTF8.GetBytes(json);

// 較佳: 直接寫入 Stream
using var stream = mmf.CreateViewStream();
using var writer = new Utf8JsonWriter(stream);
JsonSerializer.Serialize(writer, data);
```

#### 2. 重用資源

```csharp
// 保持 MemoryMappedFile 開啟，避免重複創建
private MemoryMappedFile _mmf;

public void Initialize()
{
    _mmf = MemoryMappedFile.CreateOrOpen(SegmentName, SegmentSize);
}

public void Dispose()
{
    _mmf?.Dispose();
}
```

#### 3. 批次處理

當有多筆資料時，考慮使用陣列格式：

```json
{
  "batch": [
    { "data1": "..." },
    { "data2": "..." }
  ]
}
```

### 進階應用：雙向同步範例

```csharp
// C# 完整範例：設備端處理指令並回覆確認
public class EquipmentController
{
    public void Run()
    {
        var reader = new SharedMemoryReader();
        var ackWriter = new SharedMemoryWriter();

        while (true)
        {
            // 1. 等待指令
            string commandJson = reader.WaitForCommand();
            if (commandJson == null) continue;

            // 2. 解析指令
            var command = JsonSerializer.Deserialize<EquipmentCommand>(commandJson);

            // 3. 執行指令
            bool success = ExecuteCommand(command);

            // 4. 寫入確認到 MES_EQUIPMENT_CMD_ACK
            var ack = new
            {
                commandId = command.CommandId,
                status = success ? "Success" : "Failed",
                message = success ? "指令執行成功" : "執行失敗",
                acknowledgedAt = DateTime.UtcNow
            };

            ackWriter.WriteAcknowledgment(ack, "MES_EQUIPMENT_CMD_ACK", "MES_ACK_READY");
        }
    }
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
