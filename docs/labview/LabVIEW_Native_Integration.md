# LabVIEW 原生整合指南 - 直接使用 kernel32.dll

## 📋 概述

本指南展示如何讓 LabVIEW **完全不依賴 C# DLL**,直接透過 `kernel32.dll` 的 Windows API 與 MES 中介軟體通訊。

### 優點

✅ **零相依性**: 不需要任何 .NET DLL
✅ **輕量級**: 只使用 Windows 內建 API
✅ **高效能**: 直接記憶體存取,無序列化開銷
✅ **跨版本**: LabVIEW 任何版本都可用 (支援 Call Library Function Node)

---

## 🔧 所需的 Windows API 函數

所有函數都在 `kernel32.dll` 中 (Windows 內建):

| 函數名稱 | 用途 |
|---------|------|
| `CreateFileMappingA` | 建立或開啟共享記憶體區段 |
| `OpenFileMappingA` | 開啟現有共享記憶體區段 |
| `MapViewOfFile` | 將共享記憶體映射到程序位址空間 |
| `UnmapViewOfFile` | 解除記憶體映射 |
| `CloseHandle` | 關閉控制代碼 (記憶體/事件) |
| `CreateEventA` | 建立或開啟事件物件 |
| `OpenEventA` | 開啟現有事件物件 |
| `SetEvent` | 觸發事件信號 |
| `WaitForSingleObject` | 等待事件信號 |
| `ResetEvent` | 重置事件狀態 |
| `RtlMoveMemory` | 記憶體拷貝 (讀寫資料) |

---

## 📊 LabVIEW Call Library Function 配置

### 1. CreateFileMappingA - 建立共享記憶體

**函數原型**:
```c
HANDLE CreateFileMappingA(
  HANDLE hFile,                // -1 (INVALID_HANDLE_VALUE)
  LPSECURITY_ATTRIBUTES lpAttr, // NULL (0)
  DWORD flProtect,              // PAGE_READWRITE (4)
  DWORD dwMaximumSizeHigh,      // 0
  DWORD dwMaximumSizeLow,       // 10485760 (10MB)
  LPCSTR lpName                 // "MES_INSPECTION_DATA"
);
```

**LabVIEW 配置**:

| 參數 | 類型 | 值 | 說明 |
|------|------|----|----|
| Library Name | - | `kernel32.dll` | |
| Function Name | - | `CreateFileMappingA` | |
| **Return Type** | Numeric (U32) | - | Handle |
| hFile | Numeric (I32) | -1 | INVALID_HANDLE_VALUE |
| lpAttributes | Numeric (U32) | 0 | NULL |
| flProtect | Numeric (U32) | 4 | PAGE_READWRITE |
| dwMaxSizeHigh | Numeric (U32) | 0 | 高位 (0 表示小於 4GB) |
| dwMaxSizeLow | Numeric (U32) | 10485760 | 10MB |
| lpName | String (C String Pointer) | `"MES_INSPECTION_DATA"` | 區段名稱 |

**LabVIEW 連線設定**:
- Thread: `Run in any thread` (預設)
- Calling Convention: `C` (預設)

---

### 2. OpenFileMappingA - 開啟共享記憶體

**函數原型**:
```c
HANDLE OpenFileMappingA(
  DWORD dwDesiredAccess, // FILE_MAP_READ (4) 或 FILE_MAP_WRITE (2)
  BOOL bInheritHandle,   // FALSE (0)
  LPCSTR lpName          // "MES_EQUIPMENT_CMD"
);
```

**LabVIEW 配置**:

| 參數 | 類型 | 值 | 說明 |
|------|------|----|----|
| Function Name | - | `OpenFileMappingA` | |
| **Return Type** | Numeric (U32) | - | Handle |
| dwDesiredAccess | Numeric (U32) | 4 | FILE_MAP_READ (讀取) |
| bInheritHandle | Numeric (U32) | 0 | FALSE |
| lpName | String (C String Pointer) | `"MES_EQUIPMENT_CMD"` | |

---

### 3. MapViewOfFile - 映射記憶體

**函數原型**:
```c
LPVOID MapViewOfFile(
  HANDLE hFileMappingObject, // from CreateFileMapping
  DWORD dwDesiredAccess,     // FILE_MAP_WRITE (2) 或 FILE_MAP_READ (4)
  DWORD dwFileOffsetHigh,    // 0
  DWORD dwFileOffsetLow,     // 0
  SIZE_T dwNumberOfBytes     // 10485760
);
```

**LabVIEW 配置**:

| 參數 | 類型 | 值 |
|------|------|---|
| **Return Type** | Numeric (U32) | Pointer |
| hFileMappingObject | Numeric (U32) | from CreateFileMapping |
| dwDesiredAccess | Numeric (U32) | 2 (寫) / 4 (讀) |
| dwFileOffsetHigh | Numeric (U32) | 0 |
| dwFileOffsetLow | Numeric (U32) | 0 |
| dwNumberOfBytes | Numeric (U32) | 10485760 |

---

### 4. RtlMoveMemory - 寫入/讀取資料

**函數原型**:
```c
void RtlMoveMemory(
  PVOID Destination,  // 目標位址 (pointer from MapViewOfFile)
  const VOID* Source, // 來源資料 (byte array)
  SIZE_T Length       // 資料長度
);
```

**寫入資料時 (LabVIEW → 共享記憶體)**:

| 參數 | 類型 | 說明 |
|------|------|------|
| Destination | Numeric (U32) | MapViewOfFile 回傳的指標 |
| Source | Array (1D U8[]) | 要寫入的位元組陣列 |
| Length | Numeric (U32) | 陣列長度 |

**讀取資料時 (共享記憶體 → LabVIEW)**:

| 參數 | 類型 | 說明 |
|------|------|------|
| Destination | Array (1D U8[]) | 接收資料的陣列 |
| Source | Numeric (U32) | MapViewOfFile 回傳的指標 |
| Length | Numeric (U32) | 要讀取的長度 |

---

### 5. CreateEventA - 建立事件

**函數原型**:
```c
HANDLE CreateEventA(
  LPSECURITY_ATTRIBUTES lpAttr, // NULL (0)
  BOOL bManualReset,             // FALSE (0) - Auto-reset
  BOOL bInitialState,            // FALSE (0) - Non-signaled
  LPCSTR lpName                  // "MES_DATA_READY"
);
```

**LabVIEW 配置**:

| 參數 | 類型 | 值 |
|------|------|---|
| **Return Type** | Numeric (U32) | Handle |
| lpEventAttributes | Numeric (U32) | 0 |
| bManualReset | Numeric (U32) | 0 (Auto-reset) |
| bInitialState | Numeric (U32) | 0 (Non-signaled) |
| lpName | String (C String Pointer) | `"MES_DATA_READY"` |

---

### 6. SetEvent - 觸發事件

**函數原型**:
```c
BOOL SetEvent(HANDLE hEvent);
```

**LabVIEW 配置**:

| 參數 | 類型 | 說明 |
|------|------|------|
| **Return Type** | Numeric (U32) | 非零=成功 |
| hEvent | Numeric (U32) | CreateEvent 回傳的 Handle |

---

### 7. WaitForSingleObject - 等待事件

**函數原型**:
```c
DWORD WaitForSingleObject(
  HANDLE hHandle,        // Event handle
  DWORD dwMilliseconds   // Timeout (0xFFFFFFFF = INFINITE)
);
```

**LabVIEW 配置**:

| 參數 | 類型 | 值 |
|------|------|---|
| **Return Type** | Numeric (U32) | 0=成功, 258=逾時 |
| hHandle | Numeric (U32) | Event Handle |
| dwMilliseconds | Numeric (U32) | 30000 (30秒) |

**回傳值**:
- `0` (WAIT_OBJECT_0): 事件已觸發
- `258` (WAIT_TIMEOUT): 逾時

---

### 8. CloseHandle - 關閉控制代碼

**函數原型**:
```c
BOOL CloseHandle(HANDLE hObject);
```

**LabVIEW 配置**:

| 參數 | 類型 | 說明 |
|------|------|------|
| **Return Type** | Numeric (U32) | 非零=成功 |
| hObject | Numeric (U32) | Handle (記憶體或事件) |

---

## 🎯 完整 LabVIEW VI 設計範例

### 範例 1: 寫入檢驗資料 (LabVIEW → 中介軟體)

```
┌────────────────────────────────────────────────────────────┐
│              WriteInspectionData.vi                        │
├────────────────────────────────────────────────────────────┤
│ 【輸入】                                                    │
│  - jsonString (String): JSON 格式的檢驗資料               │
│                                                            │
│ 【輸出】                                                    │
│  - success (Boolean): 是否成功                            │
│  - errorMessage (String): 錯誤訊息                        │
│                                                            │
│ 【Block Diagram】                                          │
│                                                            │
│ ┌──────────────────────────────────────┐                  │
│ │ 1. JSON String → String to Byte Array│                  │
│ │    (UTF-8 encoding)                  │                  │
│ │    ├─ Output: jsonBytes (U8[])       │                  │
│ │    └─ Output: length (I32)           │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 2. 建立長度前綴 (4 bytes)            │                  │
│ │    Type Cast: length (I32) → U8[4]   │                  │
│ │    (Little-endian)                   │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 3. 合併陣列                          │                  │
│ │    Build Array:                      │                  │
│ │    [lengthBytes] + [jsonBytes]       │                  │
│ │    = dataToWrite (U8[])              │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 4. CreateFileMappingA                │                  │
│ │    ├─ hFile: -1                      │                  │
│ │    ├─ lpAttributes: 0                │                  │
│ │    ├─ flProtect: 4 (PAGE_READWRITE)  │                  │
│ │    ├─ dwMaxSizeHigh: 0               │                  │
│ │    ├─ dwMaxSizeLow: 10485760         │                  │
│ │    └─ lpName: "MES_INSPECTION_DATA"  │                  │
│ │    → Output: hMapping (U32)          │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 5. Case Structure: hMapping != 0?    │                  │
│ │    ├─ False: Error (建立失敗)       │                  │
│ │    └─ True: 繼續                     │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 6. MapViewOfFile                     │                  │
│ │    ├─ hFileMappingObject: hMapping   │                  │
│ │    ├─ dwDesiredAccess: 2 (WRITE)     │                  │
│ │    ├─ dwFileOffsetHigh: 0            │                  │
│ │    ├─ dwFileOffsetLow: 0             │                  │
│ │    └─ dwNumberOfBytes: 10485760      │                  │
│ │    → Output: pMemory (U32 Pointer)   │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 7. RtlMoveMemory (寫入資料)          │                  │
│ │    ├─ Destination: pMemory           │                  │
│ │    ├─ Source: dataToWrite            │                  │
│ │    └─ Length: Array Size             │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 8. UnmapViewOfFile(pMemory)          │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 9. CloseHandle(hMapping)             │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 10. CreateEventA                     │                  │
│ │     ├─ lpEventAttributes: 0          │                  │
│ │     ├─ bManualReset: 0               │                  │
│ │     ├─ bInitialState: 0              │                  │
│ │     └─ lpName: "MES_DATA_READY"      │                  │
│ │     → Output: hEvent (U32)           │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 11. SetEvent(hEvent)                 │                  │
│ │     → 觸發事件通知中介軟體           │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 12. CloseHandle(hEvent)              │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 13. 輸出 success = True              │                  │
│ └──────────────────────────────────────┘                  │
│                                                            │
│ 【錯誤處理】                                                │
│  - 所有步驟包在 Error Cluster 中                          │
│  - 任何錯誤發生時直接跳到清理步驟                         │
└────────────────────────────────────────────────────────────┘
```

---

### 範例 2: 等待並讀取設備指令 (中介軟體 → LabVIEW)

```
┌────────────────────────────────────────────────────────────┐
│              WaitForCommand.vi                             │
├────────────────────────────────────────────────────────────┤
│ 【輸入】                                                    │
│  - timeoutSeconds (I32): 逾時秒數 (預設 30)               │
│                                                            │
│ 【輸出】                                                    │
│  - commandJson (String): 接收到的 JSON 指令               │
│  - timedOut (Boolean): 是否逾時                           │
│  - error (Error Cluster): 錯誤資訊                        │
│                                                            │
│ 【Block Diagram】                                          │
│                                                            │
│ ┌──────────────────────────────────────┐                  │
│ │ 1. OpenEventA                        │                  │
│ │    ├─ dwDesiredAccess: 0x001F0003    │                  │
│ │    │  (SYNCHRONIZE | EVENT_MODIFY)   │                  │
│ │    ├─ bInheritHandle: 0              │                  │
│ │    └─ lpName: "MES_CMD_READY"        │                  │
│ │    → Output: hEvent (U32)            │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 2. WaitForSingleObject               │                  │
│ │    ├─ hHandle: hEvent                │                  │
│ │    └─ dwMilliseconds: timeout*1000   │                  │
│ │    → Output: waitResult (U32)        │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 3. Case Structure: waitResult        │                  │
│ │    ├─ 0 (WAIT_OBJECT_0): 繼續讀取   │                  │
│ │    └─ 258 (WAIT_TIMEOUT): 逾時      │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓ (if signaled)                            │
│ ┌──────────────────────────────────────┐                  │
│ │ 4. OpenFileMappingA                  │                  │
│ │    ├─ dwDesiredAccess: 4 (READ)      │                  │
│ │    ├─ bInheritHandle: 0              │                  │
│ │    └─ lpName: "MES_EQUIPMENT_CMD"    │                  │
│ │    → Output: hMapping (U32)          │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 5. MapViewOfFile                     │                  │
│ │    ├─ hFileMappingObject: hMapping   │                  │
│ │    ├─ dwDesiredAccess: 4 (READ)      │                  │
│ │    ├─ dwFileOffsetHigh: 0            │                  │
│ │    ├─ dwFileOffsetLow: 0             │                  │
│ │    └─ dwNumberOfBytes: 10485760      │                  │
│ │    → Output: pMemory (U32)           │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 6. 讀取長度 (前 4 bytes)             │                  │
│ │    Initialize Array: U8[4]           │                  │
│ │    RtlMoveMemory:                    │                  │
│ │      ├─ Destination: lengthBytes[4]  │                  │
│ │      ├─ Source: pMemory              │                  │
│ │      └─ Length: 4                    │                  │
│ │    Type Cast: U8[4] → I32 (length)   │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 7. 驗證長度                          │                  │
│ │    Case: (length > 0) AND            │                  │
│ │          (length < 10485756)?        │                  │
│ │    ├─ True: 繼續讀取                 │                  │
│ │    └─ False: Error (Invalid length)  │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 8. 讀取 JSON 資料                    │                  │
│ │    Initialize Array: U8[length]      │                  │
│ │    RtlMoveMemory:                    │                  │
│ │      ├─ Destination: jsonBytes[]     │                  │
│ │      ├─ Source: pMemory + 4          │                  │
│ │      │  (指標運算: Add U32)          │                  │
│ │      └─ Length: length               │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 9. Byte Array to String (UTF-8)      │                  │
│ │    → Output: commandJson (String)    │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 10. UnmapViewOfFile(pMemory)         │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 11. CloseHandle(hMapping)            │                  │
│ └──────────────────────────────────────┘                  │
│                 ↓                                          │
│ ┌──────────────────────────────────────┐                  │
│ │ 12. CloseHandle(hEvent)              │                  │
│ └──────────────────────────────────────┘                  │
└────────────────────────────────────────────────────────────┘
```

---

## 🔄 完整主程式範例 (包含事件迴圈)

### 主程式: CommandListener.vi

```
┌────────────────────────────────────────────────────────────┐
│          CommandListener.vi (主程式)                       │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ ┌────────────────────────────────────┐                    │
│ │  【前置面板控制項】                │                    │
│ │  - Stop Button (Boolean)           │                    │
│ │  - Command Display (String)        │                    │
│ │  - Status Indicator (LED)          │                    │
│ └────────────────────────────────────┘                    │
│                                                            │
│ ┌────────────────────────────────────┐                    │
│ │  【Block Diagram - While Loop】    │                    │
│ │                                    │                    │
│ │  While Loop (條件: NOT Stop)       │                    │
│ │  ┌──────────────────────────────┐  │                    │
│ │  │                              │  │                    │
│ │  │  1. WaitForCommand.vi        │  │                    │
│ │  │     └─ timeout: 1 (秒)       │  │                    │
│ │  │                              │  │                    │
│ │  │  2. Case Structure:          │  │                    │
│ │  │     ├─ timedOut = False:     │  │                    │
│ │  │     │  ├─ 顯示 commandJson   │  │                    │
│ │  │     │  ├─ 解析 JSON          │  │                    │
│ │  │     │  ├─ 執行指令           │  │                    │
│ │  │     │  └─ 回傳確認           │  │                    │
│ │  │     │     (呼叫下方 SubVI)   │  │                    │
│ │  │     │                        │  │                    │
│ │  │     └─ timedOut = True:      │  │                    │
│ │  │        └─ 繼續等待           │  │                    │
│ │  │                              │  │                    │
│ │  │  3. Wait (ms): 100           │  │                    │
│ │  │                              │  │                    │
│ │  └──────────────────────────────┘  │                    │
│ └────────────────────────────────────┘                    │
└────────────────────────────────────────────────────────────┘
```

### SubVI: ProcessCommand.vi

```
┌────────────────────────────────────────────────────────────┐
│              ProcessCommand.vi                             │
├────────────────────────────────────────────────────────────┤
│ 【輸入】                                                    │
│  - commandJson (String): 指令 JSON                        │
│                                                            │
│ 【輸出】                                                    │
│  - success (Boolean)                                       │
│                                                            │
│ 【Block Diagram】                                          │
│                                                            │
│ 1. Unflatten JSON → Extract Fields:                       │
│    ├─ CommandId (String)                                  │
│    ├─ CommandType (String)                                │
│    └─ Parameters (Cluster)                                │
│                                                            │
│ 2. Case Structure (CommandType):                          │
│    ├─ "ChangeParameter":                                  │
│    │   └─ 執行參數變更邏輯                                │
│    ├─ "Calibrate":                                        │
│    │   └─ 執行校正流程                                    │
│    └─ "Start" / "Stop" / "Reset":                         │
│        └─ 執行對應操作                                    │
│                                                            │
│ 3. Build Acknowledgment JSON:                             │
│    {                                                       │
│      "CommandId": <CommandId>,                            │
│      "Status": "Success" or "Failed",                     │
│      "Message": "執行結果",                                │
│      "AcknowledgedAt": <Current UTC Time>                 │
│    }                                                       │
│                                                            │
│ 4. WriteCommandAck.vi (呼叫寫入 SubVI)                    │
│    └─ 區段: "MES_EQUIPMENT_CMD_ACK"                       │
│    └─ 事件: "MES_ACK_READY"                               │
└────────────────────────────────────────────────────────────┘
```

---

## 📝 JSON 處理建議

### 方法 1: LabVIEW 2019+ 原生 JSON

```labview
Flatten to JSON.vi
  └─ Input: Cluster (對應資料結構)
  └─ Output: JSON String

Unflatten From JSON.vi
  └─ Input: JSON String
  └─ Output: Cluster
```

### 方法 2: JKI JSONtext (LabVIEW < 2019)

```labview
# 安裝 VI Package Manager → 搜尋 "JSONtext"

Parse JSON.vi
  └─ Input: JSON String
  └─ Output: Variant

Flatten to JSON.vi
  └─ Input: Variant/Cluster
  └─ Output: JSON String
```

### 方法 3: 手動解析 (輕量級)

```labview
# 提取 CommandId 範例
Match Pattern:
  └─ String: commandJson
  └─ Pattern: "\"CommandId\":\s*\"([^\"]+)\""
  └─ Output: CommandId (submatch 1)
```

---

## ⚡ 效能優化技巧

### 1. 重用控制代碼 (避免重複建立)

```labview
# 錯誤做法: 每次呼叫都 Create/Close
While Loop {
  CreateFileMapping → ... → CloseHandle  // 慢!
}

# 正確做法: 迴圈外建立,迴圈內重用
CreateFileMapping (迴圈前)
  ↓
While Loop {
  MapViewOfFile → 讀寫 → UnmapViewOfFile  // 快!
}
  ↓
CloseHandle (迴圈後)
```

### 2. 使用 Shift Register 保存控制代碼

```labview
While Loop [初始化 Shift Register = 0]
  ├─ Left Shift Register: hMapping (U32)
  │
  ├─ Case: hMapping == 0?
  │   ├─ True: CreateFileMapping
  │   └─ False: 使用現有 hMapping
  │
  └─ Right Shift Register: hMapping
```

---

## 🛡️ 錯誤處理最佳實踐

### 1. 檢查所有 Handle

```labview
CreateFileMapping
  ↓
Case: Handle == 0?
  ├─ True: Error (GetLastError)
  └─ False: 繼續
```

### 2. GetLastError 函數

```c
DWORD GetLastError(void);
```

**LabVIEW Call Library 配置**:

| 參數 | 類型 |
|------|------|
| Function | `GetLastError` |
| Return Type | Numeric (U32) |

**常見錯誤碼**:
- `2`: ERROR_FILE_NOT_FOUND (區段不存在)
- `5`: ERROR_ACCESS_DENIED (權限不足)
- `87`: ERROR_INVALID_PARAMETER (參數錯誤)

---

## 📦 可重用 SubVI 庫

建議建立以下 SubVI 以簡化開發:

```
SharedMemory.lvlib
├─ CreateOrOpenSegment.vi
│  └─ Input: segmentName, size
│  └─ Output: hMapping, error
│
├─ WriteData.vi
│  └─ Input: segmentName, dataBytes[], eventName
│  └─ Output: success, error
│
├─ ReadData.vi
│  └─ Input: segmentName, timeout
│  └─ Output: dataBytes[], timedOut, error
│
├─ WaitForEvent.vi
│  └─ Input: eventName, timeout
│  └─ Output: signaled, error
│
└─ TriggerEvent.vi
   └─ Input: eventName
   └─ Output: success, error
```

---

## 🔍 除錯工具

### 1. 使用 Process Explorer

下載: https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer

**檢查共享記憶體**:
1. 執行 Process Explorer (以系統管理員)
2. Find → Find Handle → 輸入 "MES_INSPECTION_DATA"
3. 查看哪些程序持有此控制代碼

### 2. 使用 WinObj

下載: https://learn.microsoft.com/en-us/sysinternals/downloads/winobj

**檢視物件**:
1. 開啟 WinObj
2. 導航至 `\BaseNamedObjects\`
3. 搜尋 `MES_*` 查看共享記憶體和事件物件

---

## ✅ 完整檢查清單

### 開發前檢查

- [ ] LabVIEW 版本確認 (任何支援 Call Library 的版本)
- [ ] Windows 作業系統 (XP/7/10/11 皆可)
- [ ] MES 中介軟體服務執行中

### VI 建立檢查

- [ ] 所有 Call Library Function Node 配置正確
- [ ] 字串參數使用 "C String Pointer" 類型
- [ ] Handle/Pointer 使用 Numeric U32 類型
- [ ] Byte Array 使用 1D Array of U8
- [ ] 錯誤處理涵蓋所有 API 呼叫

### 測試檢查

- [ ] 測試寫入檢驗資料
- [ ] 測試接收設備指令
- [ ] 測試逾時處理
- [ ] 測試錯誤處理 (停止中介軟體服務)
- [ ] 記憶體洩漏測試 (長時間執行)

---

## 🚀 快速開始步驟

### 步驟 1: 建立 WriteInspectionData.vi

1. 新增 VI
2. 新增以下 Call Library Function Nodes:
   - CreateFileMappingA
   - MapViewOfFile
   - RtlMoveMemory
   - UnmapViewOfFile
   - CloseHandle (x2)
   - CreateEventA
   - SetEvent
3. 按照上方範例連接
4. 測試: 傳入簡單 JSON 字串

### 步驟 2: 建立 WaitForCommand.vi

1. 新增 VI
2. 新增以下 Call Library Function Nodes:
   - OpenEventA
   - WaitForSingleObject
   - OpenFileMappingA
   - MapViewOfFile
   - RtlMoveMemory (x2)
   - UnmapViewOfFile
   - CloseHandle (x2)
3. 按照上方範例連接
4. 測試: 執行並等待中介軟體發送指令

### 步驟 3: 整合到主程式

1. 建立 While Loop
2. 呼叫 WaitForCommand.vi
3. 處理接收到的 JSON
4. 執行業務邏輯
5. 回傳確認

---

## 📞 取得協助

如有問題:
1. 檢查 kernel32.dll 函數簽章是否正確
2. 使用 Process Explorer 確認共享記憶體已建立
3. 檢查中介軟體服務是否執行中
4. 驗證 JSON 格式

---

**優點總結**:
- ✅ 零 .NET 相依性
- ✅ 完全 LabVIEW 原生實作
- ✅ 高效能直接記憶體存取
- ✅ 跨 LabVIEW 版本相容

**此方案完全移除 C# DLL 相依性!** 🎉
