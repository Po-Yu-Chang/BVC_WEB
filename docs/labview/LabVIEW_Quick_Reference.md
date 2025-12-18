# LabVIEW 快速參考卡 - kernel32.dll API

## 🔧 所需的 Windows API 清單

所有函數都在 `kernel32.dll` 中 (不需額外 DLL)

### 共享記憶體操作

| API | 用途 | 回傳類型 |
|-----|------|---------|
| `CreateFileMappingA` | 建立共享記憶體區段 | Handle (U32) |
| `OpenFileMappingA` | 開啟現有區段 | Handle (U32) |
| `MapViewOfFile` | 映射到記憶體位址 | Pointer (U32) |
| `UnmapViewOfFile` | 解除映射 | Bool (U32) |
| `RtlMoveMemory` | 讀寫記憶體 | void |

### 事件同步操作

| API | 用途 | 回傳類型 |
|-----|------|---------|
| `CreateEventA` | 建立事件物件 | Handle (U32) |
| `OpenEventA` | 開啟現有事件 | Handle (U32) |
| `SetEvent` | 觸發事件信號 | Bool (U32) |
| `WaitForSingleObject` | 等待事件信號 | Result (U32) |

### 資源管理

| API | 用途 | 回傳類型 |
|-----|------|---------|
| `CloseHandle` | 關閉 Handle | Bool (U32) |
| `GetLastError` | 取得錯誤碼 | ErrorCode (U32) |

---

## 📊 Call Library Function 快速配置表

### CreateFileMappingA

```
Library: kernel32.dll
Function: CreateFileMappingA
Return: Numeric U32

參數:
┌─────────────────┬───────────┬────────────────┐
│ 參數名           │ 類型      │ 典型值         │
├─────────────────┼───────────┼────────────────┤
│ hFile           │ I32       │ -1             │
│ lpAttributes    │ U32       │ 0              │
│ flProtect       │ U32       │ 4              │
│ dwMaxSizeHigh   │ U32       │ 0              │
│ dwMaxSizeLow    │ U32       │ 10485760       │
│ lpName          │ CStr Ptr  │ "MES_INSPEC…"  │
└─────────────────┴───────────┴────────────────┘
```

### MapViewOfFile

```
Library: kernel32.dll
Function: MapViewOfFile
Return: Numeric U32 (Pointer)

參數:
┌─────────────────────┬───────────┬────────────────┐
│ 參數名               │ 類型      │ 典型值         │
├─────────────────────┼───────────┼────────────────┤
│ hFileMappingObject  │ U32       │ from Create… │
│ dwDesiredAccess     │ U32       │ 2 (寫) / 4 (讀)│
│ dwFileOffsetHigh    │ U32       │ 0              │
│ dwFileOffsetLow     │ U32       │ 0              │
│ dwNumberOfBytes     │ U32       │ 10485760       │
└─────────────────────┴───────────┴────────────────┘
```

### RtlMoveMemory (寫入)

```
Library: kernel32.dll
Function: RtlMoveMemory
Return: (none)

參數:
┌─────────────┬───────────┬────────────────┐
│ 參數名       │ 類型      │ 說明           │
├─────────────┼───────────┼────────────────┤
│ Destination │ U32       │ 記憶體指標     │
│ Source      │ U8[]      │ 位元組陣列     │
│ Length      │ U32       │ 陣列長度       │
└─────────────┴───────────┴────────────────┘
```

### RtlMoveMemory (讀取)

```
Library: kernel32.dll
Function: RtlMoveMemory
Return: (none)

參數:
┌─────────────┬───────────┬────────────────┐
│ 參數名       │ 類型      │ 說明           │
├─────────────┼───────────┼────────────────┤
│ Destination │ U8[]      │ 接收陣列       │
│ Source      │ U32       │ 記憶體指標     │
│ Length      │ U32       │ 讀取長度       │
└─────────────┴───────────┴────────────────┘
```

### CreateEventA

```
Library: kernel32.dll
Function: CreateEventA
Return: Numeric U32 (Handle)

參數:
┌───────────────────┬───────────┬────────────────┐
│ 參數名             │ 類型      │ 典型值         │
├───────────────────┼───────────┼────────────────┤
│ lpEventAttributes │ U32       │ 0              │
│ bManualReset      │ U32       │ 0 (Auto-reset) │
│ bInitialState     │ U32       │ 0 (Non-signal) │
│ lpName            │ CStr Ptr  │ "MES_DATA_…"   │
└───────────────────┴───────────┴────────────────┘
```

### WaitForSingleObject

```
Library: kernel32.dll
Function: WaitForSingleObject
Return: Numeric U32 (Result)

參數:
┌─────────────────┬───────────┬────────────────┐
│ 參數名           │ 類型      │ 典型值         │
├─────────────────┼───────────┼────────────────┤
│ hHandle         │ U32       │ Event Handle   │
│ dwMilliseconds  │ U32       │ 30000 (30秒)   │
└─────────────────┴───────────┴────────────────┘

回傳值:
  0 (WAIT_OBJECT_0) = 事件觸發
  258 (WAIT_TIMEOUT) = 逾時
```

### SetEvent

```
Library: kernel32.dll
Function: SetEvent
Return: Numeric U32 (Bool)

參數:
┌─────────────┬───────────┬────────────────┐
│ 參數名       │ 類型      │ 說明           │
├─────────────┼───────────┼────────────────┤
│ hEvent      │ U32       │ Event Handle   │
└─────────────┴───────────┴────────────────┘
```

### CloseHandle

```
Library: kernel32.dll
Function: CloseHandle
Return: Numeric U32 (Bool)

參數:
┌─────────────┬───────────┬────────────────┐
│ 參數名       │ 類型      │ 說明           │
├─────────────┼───────────┼────────────────┤
│ hObject     │ U32       │ 任何 Handle    │
└─────────────┴───────────┴────────────────┘
```

---

## 🎯 常數定義

### 記憶體保護常數

```
PAGE_READONLY       = 2
PAGE_READWRITE      = 4
PAGE_WRITECOPY      = 8
```

### 檔案映射存取常數

```
FILE_MAP_READ       = 4
FILE_MAP_WRITE      = 2
FILE_MAP_ALL_ACCESS = 0xF001F
```

### 事件存取權限

```
SYNCHRONIZE         = 0x00100000
EVENT_MODIFY_STATE  = 0x0002
EVENT_ALL_ACCESS    = 0x001F0003
```

### 等待結果常數

```
WAIT_OBJECT_0       = 0
WAIT_TIMEOUT        = 258
WAIT_FAILED         = 0xFFFFFFFF
```

### 錯誤碼

```
ERROR_SUCCESS           = 0
ERROR_FILE_NOT_FOUND    = 2
ERROR_ACCESS_DENIED     = 5
ERROR_INVALID_HANDLE    = 6
ERROR_INVALID_PARAMETER = 87
ERROR_ALREADY_EXISTS    = 183
```

---

## 📝 共享記憶體區段名稱

### MES 中介軟體使用的區段

| 區段名稱 | 大小 | 用途 | 事件名稱 |
|---------|------|------|---------|
| `MES_INSPECTION_DATA` | 10 MB | 檢驗資料 (LabVIEW → 中介軟體) | `MES_DATA_READY` |
| `MES_EQUIPMENT_CMD` | 10 MB | 設備指令 (中介軟體 → LabVIEW) | `MES_CMD_READY` |
| `MES_EQUIPMENT_CMD_ACK` | 10 MB | 指令確認 (LabVIEW → 中介軟體) | `MES_ACK_READY` |

---

## 🔄 資料格式

### 共享記憶體布局

```
┌───────────────────────────────────────┐
│ Byte 0-3:  資料長度 (int32, LE)       │  4 bytes
├───────────────────────────────────────┤
│ Byte 4-N:  UTF-8 JSON 字串            │  N bytes
└───────────────────────────────────────┘
```

### LabVIEW 資料型態對應

| Windows 類型 | LabVIEW 類型 | 說明 |
|-------------|-------------|------|
| HANDLE | Numeric U32 | 控制代碼 |
| LPVOID | Numeric U32 | 指標 |
| DWORD | Numeric U32 | 無符號 32 位元 |
| BOOL | Numeric U32 | 布林值 (0/非0) |
| LPCSTR | String (C String Pointer) | C 字串指標 |
| int | Numeric I32 | 有符號 32 位元 |
| BYTE[] | Array (1D U8) | 位元組陣列 |

---

## ⚡ 最小可行範例

### 寫入資料 (5 步驟)

```labview
1. String to Byte Array (UTF-8)
2. Type Cast: I32 (length) → U8[4]
3. Build Array: [lengthBytes] + [dataBytes]
4. CreateFileMapping + MapViewOfFile
5. RtlMoveMemory (寫入)
6. UnmapViewOfFile + CloseHandle
7. CreateEvent + SetEvent + CloseHandle
```

### 讀取資料 (5 步驟)

```labview
1. OpenEvent + WaitForSingleObject
2. OpenFileMapping + MapViewOfFile
3. RtlMoveMemory (讀 4 bytes → length)
4. RtlMoveMemory (讀 N bytes → data)
5. Byte Array to String (UTF-8)
6. UnmapViewOfFile + CloseHandle (x2)
```

---

## 🛡️ 錯誤處理檢查點

### 必須檢查的回傳值

```labview
✓ CreateFileMapping 回傳 != 0
✓ OpenFileMapping 回傳 != 0
✓ MapViewOfFile 回傳 != 0
✓ CreateEvent 回傳 != 0
✓ WaitForSingleObject 回傳 == 0 (WAIT_OBJECT_0)
✓ 讀取的資料長度 > 0 且 < 10MB
```

### 資源清理順序

```
1. UnmapViewOfFile (先解除映射)
2. CloseHandle (記憶體)
3. CloseHandle (事件)
```

---

## 📋 JSON 模板

### 檢驗資料 (最小版本)

```json
{
  "RowNo": "ROW_001",
  "ProcName": "檢驗流程",
  "DevName": "設備名",
  "UserName": "操作員",
  "WorkClass": "班別",
  "TraceCode": "追蹤碼",
  "ParamData": [],
  "Benchmarks": [],
  "OtherData": [],
  "InspectionTime": "2025-01-17T12:00:00Z"
}
```

### 指令確認 (回覆中介軟體)

```json
{
  "CommandId": "從指令中取得的 GUID",
  "Status": "Success",
  "Message": "執行成功",
  "AcknowledgedAt": "2025-01-17T12:00:05Z"
}
```

---

## 🔍 除錯技巧

### 1. 驗證 Handle 是否有效

```labview
Case Structure: Handle == 0?
  ├─ True:
  │   └─ GetLastError → 顯示錯誤碼
  └─ False:
      └─ 繼續執行
```

### 2. 驗證共享記憶體存在

使用 Process Explorer:
1. 下載: https://learn.microsoft.com/sysinternals
2. Find → Find Handle → 輸入 "MES_INSPECTION_DATA"
3. 檢查是否被中介軟體持有

### 3. 驗證事件觸發

```labview
WaitForSingleObject (timeout = 1000)
  ├─ Return = 0: 事件已觸發 ✓
  └─ Return = 258: 逾時 (事件未觸發)
```

---

## 📦 建議的 LabVIEW 專案結構

```
MyProject.lvproj
├─ Library: SharedMemoryAPI.lvlib
│  ├─ CreateSharedMemory.vi
│  ├─ WriteSharedMemory.vi
│  ├─ ReadSharedMemory.vi
│  ├─ WaitForEvent.vi
│  └─ TriggerEvent.vi
│
├─ Library: MESAPI.lvlib
│  ├─ WriteInspectionData.vi
│  ├─ WaitForCommand.vi
│  ├─ WriteCommandAck.vi
│  └─ ParseJSON.vi
│
└─ Main.vi
   └─ CommandListener.vi (主程式)
```

---

## ⚙️ 效能優化

### ✅ 好習慣

```labview
# 1. 重用 Handle (迴圈外建立)
Before Loop: CreateFileMapping
  ↓
While Loop: MapViewOfFile → Use → Unmap
  ↓
After Loop: CloseHandle

# 2. 使用 Shift Register 保存 Handle
While Loop [Shift Register: hMapping]
  ├─ Case: hMapping == 0? → Create
  └─ Case: hMapping != 0? → Reuse
```

### ❌ 壞習慣

```labview
# 1. 每次都建立/關閉 (慢!)
While Loop {
  CreateFileMapping
  MapViewOfFile
  Use
  UnmapViewOfFile
  CloseHandle  ← 效能差!
}

# 2. 未檢查 Handle
CreateFileMapping → 直接使用 (可能 0!)
```

---

## 🚀 快速開始 3 步驟

### 步驟 1: 建立第一個 Call Library Node

1. Block Diagram → Functions → Connectivity → Libraries & Executables → Call Library Function Node
2. 雙擊 → Configure
3. Library: `kernel32.dll`
4. Function: `CreateFileMappingA`
5. 按照上方表格設定參數

### 步驟 2: 測試寫入資料

1. 建立簡單 JSON 字串: `{"test": "hello"}`
2. 轉換為 Byte Array
3. 呼叫 CreateFileMapping + MapViewOfFile + RtlMoveMemory
4. 檢查是否成功 (Handle != 0)

### 步驟 3: 整合到主程式

1. 建立 While Loop
2. 新增 Stop Button
3. 呼叫 WriteInspectionData.vi
4. 測試與中介軟體通訊

---

## 📞 常見問題

### Q: 如何知道寫入成功?

**A**: 檢查所有 Handle != 0,並且 CloseHandle 回傳非 0。

### Q: JSON 如何轉 UTF-8 Byte Array?

**A**: String Palette → String to Byte Array (指定 UTF-8 encoding)。

### Q: 如何處理指標運算 (Pointer + 4)?

**A**: Add Function: `pMemory (U32) + 4 (U32) = newPointer (U32)`。

### Q: 中介軟體未收到資料?

**A**: 確認:
1. CreateFileMapping 成功 (Handle != 0)
2. RtlMoveMemory 已執行
3. SetEvent 已觸發
4. 中介軟體服務執行中

---

**打印此卡片並貼在螢幕旁邊以便快速查閱!** 📌
