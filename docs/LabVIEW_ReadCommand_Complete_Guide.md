# LabVIEW 讀取設備指令完整指南 (WaitForCommand.vi)

## 📋 概述

本文檔提供 **完整且正確** 的 LabVIEW VI 設計,用於從共享記憶體讀取中介軟體發送的設備指令。

---

## 🎯 功能說明

**VI 名稱**: `WaitForCommand.vi`

**用途**: 等待並接收來自 MES 中介軟體的設備指令

**資料流向**: 中介軟體 → 共享記憶體 (`MES_EQUIPMENT_CMD`) → LabVIEW

**事件同步**: 使用 `MES_CMD_READY` EventWaitHandle

---

## 📊 輸入/輸出參數

### 輸入

| 參數名稱 | 類型 | 預設值 | 說明 |
|---------|------|-------|------|
| `timeoutSeconds` | I32 | 30 | 等待逾時秒數 (0 = 立即返回, -1 = 無限等待) |

### 輸出

| 參數名稱 | 類型 | 說明 |
|---------|------|------|
| `commandJson` | String | 接收到的 JSON 指令 (UTF-8) |
| `timedOut` | Boolean | True = 逾時未收到指令, False = 成功收到 |
| `error` | Error Cluster | 錯誤資訊 (包含錯誤碼和訊息) |

---

## 🔧 完整 Block Diagram 實作

### 整體流程圖

```
┌────────────────────────────────────────────────────────────┐
│              WaitForCommand.vi (完整版)                    │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 1: 開啟或建立事件】                                 │
│                                                            │
│   Call Library: CreateEventA ✅ (改用 Create 更安全)      │
│     ├─ lpEventAttributes: 0                               │
│     │  (Unsigned Pointer-sized Integer)                   │
│     ├─ bManualReset: 0 (U32) [Auto-reset]                 │
│     ├─ bInitialState: 0 (U32) [Non-signaled]              │
│     └─ lpName: "MES_CMD_READY" (C String Ptr)             │
│     ↓                                                      │
│   Output: hEvent (Unsigned Pointer-sized Integer) ✅      │
│     ↓                                                      │
│   Case Structure: hEvent != 0?                            │
│     ├─ False: Error "無法建立事件" → Exit                 │
│     └─ True: 繼續                                         │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 2: 等待事件信號】                                   │
│                                                            │
│   Multiply: timeoutSeconds * 1000 → timeoutMs (U32)       │
│     ↓                                                      │
│   Call Library: WaitForSingleObject ✅                    │
│     ├─ hHandle: hEvent                                    │
│     │  (Unsigned Pointer-sized Integer)                   │
│     └─ dwMilliseconds: timeoutMs (U32)                    │
│     ↓                                                      │
│   Output: waitResult (U32)                                │
│     ↓                                                      │
│   Case Structure: waitResult                              │
│     ├─ 0 (WAIT_OBJECT_0): 事件觸發 → 繼續讀取            │
│     ├─ 258 (WAIT_TIMEOUT): 逾時 → 設定 timedOut=True     │
│     └─ 其他: Error → Exit                                 │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 3: 開啟共享記憶體】(只在事件觸發時執行)             │
│                                                            │
│   Call Library: CreateFileMappingA ✅                     │
│     ├─ hFile: -1 (Signed Pointer-sized Integer)          │
│     ├─ lpAttributes: 0 (Unsigned Pointer-sized Integer)   │
│     ├─ flProtect: 4 (U32) [PAGE_READWRITE]                │
│     ├─ dwMaxSizeHigh: 0 (U32)                             │
│     ├─ dwMaxSizeLow: 10485760 (U32) [10 MB]               │
│     └─ lpName: "MES_EQUIPMENT_CMD" (C String Ptr)         │
│     ↓                                                      │
│   Output: hMapping (Unsigned Pointer-sized Integer) ✅    │
│     ↓                                                      │
│   Case Structure: hMapping != 0?                          │
│     ├─ False: Error "無法開啟共享記憶體" → Exit           │
│     └─ True: 繼續                                         │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 4: 映射記憶體 (唯讀模式)】                          │
│                                                            │
│   Call Library: MapViewOfFile ✅                          │
│     ├─ hFileMappingObject: hMapping                       │
│     │  (Unsigned Pointer-sized Integer)                   │
│     ├─ dwDesiredAccess: 4 (U32) [FILE_MAP_READ]           │
│     ├─ dwFileOffsetHigh: 0 (U32)                          │
│     ├─ dwFileOffsetLow: 0 (U32)                           │
│     └─ dwNumberOfBytes: 10485760 (U32)                    │
│     ↓                                                      │
│   Output: pMemory (Unsigned Pointer-sized Integer) ✅     │
│     ↓                                                      │
│   Case Structure: pMemory != 0?                           │
│     ├─ False: Error "無法映射記憶體" → 清理並 Exit        │
│     └─ True: 繼續                                         │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 5: 讀取資料長度 (前 4 bytes)】                      │
│                                                            │
│   Initialize Array: Create U8[4] filled with 0            │
│     ↓                                                      │
│   Call Library: RtlMoveMemory ✅                          │
│     ├─ Destination: lengthBytes[] (Array U8[4])           │
│     │  - Type: Array U8                                   │
│     │  - Format: Array Data Pointer ⚠️                    │
│     │  - Mechanism: Pass by Reference                     │
│     ├─ Source: pMemory                                    │
│     │  (Unsigned Pointer-sized Integer)                   │
│     └─ Length: 4 (U32)                                    │
│     ↓                                                      │
│   Type Cast: U8[4] → I32 (little-endian)                  │
│     └─ Output: dataLength (I32)                           │
│     ↓                                                      │
│   Validation: (dataLength > 0) AND                        │
│               (dataLength < 10485756)?                    │
│     ├─ False: Error "無效的資料長度" → 清理並 Exit        │
│     └─ True: 繼續                                         │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 6: 讀取 JSON 資料】                                 │
│                                                            │
│   Initialize Array: Create U8[dataLength] filled with 0   │
│     ↓                                                      │
│   Add: pMemory + 4 → pJsonData                            │
│     (指標運算: 跳過前 4 bytes 長度欄位)                   │
│     ↓                                                      │
│   Call Library: RtlMoveMemory ✅                          │
│     ├─ Destination: jsonBytes[] (Array U8[dataLength])    │
│     │  - Type: Array U8                                   │
│     │  - Format: Array Data Pointer ⚠️                    │
│     │  - Mechanism: Pass by Reference                     │
│     ├─ Source: pJsonData                                  │
│     │  (Unsigned Pointer-sized Integer)                   │
│     └─ Length: dataLength (U32)                           │
│     ↓                                                      │
│   Byte Array to String (UTF-8) ✅                         │
│     └─ Output: commandJson (String)                       │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 7: 清理資源】(無論成功或失敗都要執行)               │
│                                                            │
│   Try-Finally Structure:                                  │
│     ↓                                                      │
│   If pMemory != 0:                                        │
│     └─ Call Library: UnmapViewOfFile(pMemory) ✅          │
│     ↓                                                      │
│   If hMapping != 0:                                       │
│     └─ Call Library: CloseHandle(hMapping) ✅             │
│     ↓                                                      │
│   If hEvent != 0:                                         │
│     └─ Call Library: CloseHandle(hEvent) ✅               │
│                                                            │
└────────────────────────────────────────────────────────────┘

完成! 成功讀取指令或回傳逾時狀態 ✅
```

---

## 🔧 詳細的 Call Library Function 配置

### 1. CreateEventA (建立/開啟事件)

```
┌─────────────────────────────────────────────────────────┐
│ Library: kernel32.dll                                   │
│ Function: CreateEventA                                  │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type:                                            │
│   Unsigned Pointer-sized Integer (Event Handle) ✅      │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
│                                                         │
│ 1. lpEventAttributes                                    │
│    Type: Unsigned Pointer-sized Integer ✅              │
│    Value: 0 (NULL)                                      │
│                                                         │
│ 2. bManualReset                                         │
│    Type: Numeric U32                                    │
│    Value: 0 (Auto-reset event)                          │
│                                                         │
│ 3. bInitialState                                        │
│    Type: Numeric U32                                    │
│    Value: 0 (Non-signaled)                              │
│                                                         │
│ 4. lpName                                               │
│    Type: String (C String Pointer)                      │
│    Value: "MES_CMD_READY"                               │
└─────────────────────────────────────────────────────────┘
```

### 2. WaitForSingleObject (等待事件)

```
┌─────────────────────────────────────────────────────────┐
│ Library: kernel32.dll                                   │
│ Function: WaitForSingleObject                           │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: Numeric U32 (Wait Result)                  │
│   0 = WAIT_OBJECT_0 (成功)                              │
│   258 = WAIT_TIMEOUT (逾時)                             │
│   0xFFFFFFFF = WAIT_FAILED (失敗)                       │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
│                                                         │
│ 1. hHandle                                              │
│    Type: Unsigned Pointer-sized Integer ✅              │
│    Value: hEvent (from CreateEventA)                    │
│                                                         │
│ 2. dwMilliseconds                                       │
│    Type: Numeric U32                                    │
│    Value: timeoutSeconds * 1000                         │
│    Special: 0xFFFFFFFF = INFINITE (永久等待)            │
└─────────────────────────────────────────────────────────┘

【LabVIEW 實作提示】
使用 Case Structure 判斷回傳值:
- Case 0: 事件觸發,繼續讀取資料
- Case 258: 逾時,設定 timedOut = True,跳到清理
- Default: 錯誤,產生 Error
```

### 3. CreateFileMappingA (開啟共享記憶體)

```
┌─────────────────────────────────────────────────────────┐
│ Library: kernel32.dll                                   │
│ Function: CreateFileMappingA                            │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type:                                            │
│   Unsigned Pointer-sized Integer (Mapping Handle) ✅    │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
│                                                         │
│ 1. hFile                                                │
│    Type: Signed Pointer-sized Integer ✅                │
│    Value: -1 (INVALID_HANDLE_VALUE)                     │
│                                                         │
│ 2. lpAttributes                                         │
│    Type: Unsigned Pointer-sized Integer ✅              │
│    Value: 0 (NULL)                                      │
│                                                         │
│ 3. flProtect                                            │
│    Type: Numeric U32                                    │
│    Value: 4 (PAGE_READWRITE)                            │
│                                                         │
│ 4. dwMaximumSizeHigh                                    │
│    Type: Numeric U32                                    │
│    Value: 0                                             │
│                                                         │
│ 5. dwMaximumSizeLow                                     │
│    Type: Numeric U32                                    │
│    Value: 10485760 (10 MB)                              │
│                                                         │
│ 6. lpName                                               │
│    Type: String (C String Pointer)                      │
│    Value: "MES_EQUIPMENT_CMD"                           │
└─────────────────────────────────────────────────────────┘

⚠️ 注意: 使用 CreateFileMappingA 而非 OpenFileMappingA
原因: 更安全,無論中介軟體是否先啟動都能正常工作
```

### 4. MapViewOfFile (映射記憶體 - 唯讀)

```
┌─────────────────────────────────────────────────────────┐
│ Library: kernel32.dll                                   │
│ Function: MapViewOfFile                                 │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type:                                            │
│   Unsigned Pointer-sized Integer (Memory Pointer) ✅    │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
│                                                         │
│ 1. hFileMappingObject                                   │
│    Type: Unsigned Pointer-sized Integer ✅              │
│    Value: hMapping (from CreateFileMappingA)            │
│                                                         │
│ 2. dwDesiredAccess                                      │
│    Type: Numeric U32                                    │
│    Value: 4 (FILE_MAP_READ) ← 唯讀模式 ✅               │
│                                                         │
│ 3. dwFileOffsetHigh                                     │
│    Type: Numeric U32                                    │
│    Value: 0                                             │
│                                                         │
│ 4. dwFileOffsetLow                                      │
│    Type: Numeric U32                                    │
│    Value: 0                                             │
│                                                         │
│ 5. dwNumberOfBytesToMap                                 │
│    Type: Numeric U32                                    │
│    Value: 10485760 (10 MB,與區段大小一致)               │
└─────────────────────────────────────────────────────────┘
```

### 5. RtlMoveMemory (讀取長度 - 前 4 bytes)

```
┌─────────────────────────────────────────────────────────┐
│ Library: kernel32.dll                                   │
│ Function: RtlMoveMemory                                 │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: (none/void)                                │
├─────────────────────────────────────────────────────────┤
│ Parameters (讀取模式):                                  │
│                                                         │
│ 1. Destination (接收長度的陣列)                         │
│    Type: Array                                          │
│      - Data Type: Unsigned 8-bit Integer (U8)           │
│      - Array Format: Array Data Pointer ⚠️ 關鍵!        │
│      - Mechanism: Pass by Reference                     │
│    Value: lengthBytes[4] (已初始化的 4 元素陣列)        │
│                                                         │
│ 2. Source (記憶體指標)                                  │
│    Type: Unsigned Pointer-sized Integer ✅              │
│    Value: pMemory (from MapViewOfFile)                  │
│                                                         │
│ 3. Length                                               │
│    Type: Numeric U32                                    │
│    Value: 4 (只讀取前 4 bytes)                          │
└─────────────────────────────────────────────────────────┘

【LabVIEW 前置步驟】
1. Initialize Array: Size = 4, Element = 0 (U8)
   → 建立 lengthBytes[4] 陣列
2. 連接到 RtlMoveMemory 的 Destination 參數
3. 呼叫後,lengthBytes[] 會包含長度資料
4. Type Cast: U8[4] → I32 (little-endian)
```

### 6. RtlMoveMemory (讀取 JSON 資料)

```
┌─────────────────────────────────────────────────────────┐
│ Library: kernel32.dll                                   │
│ Function: RtlMoveMemory                                 │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: (none/void)                                │
├─────────────────────────────────────────────────────────┤
│ Parameters (讀取模式):                                  │
│                                                         │
│ 1. Destination (接收 JSON 資料的陣列)                   │
│    Type: Array                                          │
│      - Data Type: Unsigned 8-bit Integer (U8)           │
│      - Array Format: Array Data Pointer ⚠️ 關鍵!        │
│      - Mechanism: Pass by Reference                     │
│    Value: jsonBytes[dataLength]                         │
│          (已初始化為 dataLength 大小的陣列)             │
│                                                         │
│ 2. Source (記憶體指標 + 4)                              │
│    Type: Unsigned Pointer-sized Integer ✅              │
│    Value: pMemory + 4                                   │
│          (指標運算: 跳過前 4 bytes 長度欄位)            │
│                                                         │
│ 3. Length                                               │
│    Type: Numeric U32                                    │
│    Value: dataLength (從步驟 5 讀取的長度)              │
└─────────────────────────────────────────────────────────┘

【LabVIEW 前置步驟】
1. Initialize Array: Size = dataLength, Element = 0 (U8)
   → 建立 jsonBytes[dataLength] 陣列
2. Add Function: pMemory (uPtr) + 4 (U32) = pJsonData (uPtr)
   → 計算 JSON 資料的起始位址
3. 連接到 RtlMoveMemory 的 Source 參數
4. 呼叫後,jsonBytes[] 會包含 JSON 資料
5. Byte Array to String (UTF-8): jsonBytes[] → commandJson
```

### 7. UnmapViewOfFile (解除記憶體映射)

```
┌─────────────────────────────────────────────────────────┐
│ Library: kernel32.dll                                   │
│ Function: UnmapViewOfFile                               │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: Numeric U32 (BOOL, 非零 = 成功)            │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
│                                                         │
│ 1. lpBaseAddress                                        │
│    Type: Unsigned Pointer-sized Integer ✅              │
│    Value: pMemory (from MapViewOfFile)                  │
└─────────────────────────────────────────────────────────┘
```

### 8. CloseHandle (關閉控制代碼)

```
┌─────────────────────────────────────────────────────────┐
│ Library: kernel32.dll                                   │
│ Function: CloseHandle                                   │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: Numeric U32 (BOOL, 非零 = 成功)            │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
│                                                         │
│ 1. hObject                                              │
│    Type: Unsigned Pointer-sized Integer ✅              │
│    Value: hMapping 或 hEvent                            │
│                                                         │
│ 使用時機:                                               │
│ - CloseHandle(hMapping) - 關閉記憶體映射 Handle         │
│ - CloseHandle(hEvent) - 關閉事件 Handle                 │
└─────────────────────────────────────────────────────────┘
```

---

## 🎨 LabVIEW Front Panel 設計

### 控制項 (Controls)

```
┌────────────────────────────────────┐
│ Timeout (seconds)                  │
│ ┌────────────────────────────────┐ │
│ │ Numeric (I32)                  │ │
│ │ Default: 30                    │ │
│ │ Range: -1 (無限) 到 3600       │ │
│ └────────────────────────────────┘ │
└────────────────────────────────────┘
```

### 指示器 (Indicators)

```
┌────────────────────────────────────────────────────┐
│ Command JSON                                       │
│ ┌────────────────────────────────────────────────┐ │
│ │ String (Multiline)                             │ │
│ │ Shows: Received JSON command                   │ │
│ │ Empty if timed out or error                    │ │
│ └────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────┘

┌────────────────────────────────────┐
│ Timed Out                          │
│ ┌────────────────────────────────┐ │
│ │ Boolean LED                    │ │
│ │ Green: Received command        │ │
│ │ Red: Timed out                 │ │
│ └────────────────────────────────┘ │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│ Error Out                          │
│ ┌────────────────────────────────┐ │
│ │ Error Cluster                  │ │
│ │ - Status (Boolean)             │ │
│ │ - Code (I32)                   │ │
│ │ - Source (String)              │ │
│ └────────────────────────────────┘ │
└────────────────────────────────────┘
```

---

## 🔄 錯誤處理流程

### Try-Finally 結構實作

```labview
┌────────────────────────────────────────────────────┐
│ Flat Sequence Structure (3 Frames)                │
├────────────────────────────────────────────────────┤
│                                                    │
│ Frame 0: 初始化變數                                │
│   hEvent = 0 (uPtr)                                │
│   hMapping = 0 (uPtr)                              │
│   pMemory = 0 (uPtr)                               │
│                                                    │
│ Frame 1: 主要邏輯 (Try Block)                      │
│   ┌─ CreateEventA → hEvent                        │
│   ├─ WaitForSingleObject                          │
│   ├─ If signaled:                                 │
│   │   ├─ CreateFileMappingA → hMapping            │
│   │   ├─ MapViewOfFile → pMemory                  │
│   │   ├─ RtlMoveMemory (read length)              │
│   │   ├─ RtlMoveMemory (read JSON)                │
│   │   └─ Byte Array to String                     │
│   └─ 任何錯誤發生時,Error Cluster 傳遞到 Frame 2  │
│                                                    │
│ Frame 2: 清理資源 (Finally Block)                  │
│   ┌─ If pMemory != 0:                             │
│   │   └─ UnmapViewOfFile(pMemory)                 │
│   ├─ If hMapping != 0:                            │
│   │   └─ CloseHandle(hMapping)                    │
│   └─ If hEvent != 0:                              │
│       └─ CloseHandle(hEvent)                      │
│                                                    │
│   ⚠️ 無論是否有錯誤,這個 Frame 都會執行           │
└────────────────────────────────────────────────────┘
```

### 使用 Shift Register 保存 Handle

```labview
While Loop [Left Shift Register: hEvent, hMapping, pMemory = 0]
  │
  ├─ Main Logic
  │   ├─ 更新 hEvent, hMapping, pMemory
  │   └─ 如果發生錯誤,保持現有值
  │
  ├─ Right Shift Register: 傳遞 Handle 到下一次迴圈
  │
  └─ 迴圈結束後:
      └─ 呼叫清理程式碼 (使用最終的 Handle 值)
```

---

## 📊 資料驗證

### 長度欄位驗證

```labview
Case Structure: Validate Data Length
  ├─ (dataLength > 0) AND (dataLength < 10485756)?
  │   ├─ True: 繼續讀取 JSON
  │   └─ False: 產生錯誤
  │       ├─ Error Code: 1001 (自定義)
  │       └─ Error Message: "Invalid data length: " + String(dataLength)
  │
  └─ 為什麼是 10485756?
      └─ 10 MB (10485760) - 4 bytes (長度欄位) = 10485756
```

---

## 🧪 測試範例

### 測試 VI: TestWaitForCommand.vi

```labview
┌────────────────────────────────────────────────────┐
│              TestWaitForCommand.vi                 │
├────────────────────────────────────────────────────┤
│                                                    │
│ While Loop (Stop Button):                         │
│   │                                                │
│   ├─ WaitForCommand.vi                            │
│   │   ├─ timeout: 5 (秒)                          │
│   │   ├─ → commandJson                            │
│   │   ├─ → timedOut                               │
│   │   └─ → error                                  │
│   │                                                │
│   ├─ Case Structure: error.status?                │
│   │   ├─ False: 檢查 timedOut                     │
│   │   │   ├─ False: 顯示 commandJson              │
│   │   │   │   └─ "Received: " + commandJson       │
│   │   │   └─ True: 顯示 "等待逾時,繼續..."        │
│   │   │                                            │
│   │   └─ True: 顯示錯誤訊息                       │
│   │       └─ "Error: " + error.source             │
│   │                                                │
│   └─ Wait (ms): 100                                │
│                                                    │
│ Loop End                                           │
└────────────────────────────────────────────────────┘

測試步驟:
1. 啟動中介軟體服務
2. 執行此 VI
3. 使用中介軟體 WPF Monitor 發送測試指令
4. 觀察 LabVIEW 是否收到 JSON 指令
```

---

## 📋 檢查清單

### 實作前檢查

- [ ] 所有 Call Library Function Node 的 Calling Convention 都是 `stdcall`
- [ ] 所有 Handle/Pointer 都是 `Unsigned Pointer-sized Integer`
- [ ] RtlMoveMemory 的 Array 參數都是 `Array Data Pointer`
- [ ] 初始化陣列大小正確 (lengthBytes[4], jsonBytes[dataLength])
- [ ] 指標運算正確 (pMemory + 4)

### 測試前檢查

- [ ] 中介軟體服務正在執行
- [ ] 共享記憶體區段 `MES_EQUIPMENT_CMD` 已建立 (使用 Process Explorer 確認)
- [ ] 事件 `MES_CMD_READY` 已建立
- [ ] VI 的 Error Cluster 已連接到所有 Call Library 節點

### 執行時檢查

- [ ] hEvent != 0 (CreateEventA 成功)
- [ ] WaitForSingleObject 回傳 0 或 258 (不是其他值)
- [ ] hMapping != 0 (CreateFileMappingA 成功)
- [ ] pMemory != 0 (MapViewOfFile 成功)
- [ ] dataLength 合理 (> 0 且 < 10 MB)
- [ ] commandJson 是有效的 UTF-8 字串

---

## 🎯 完整範例: 主迴圈整合

### CommandListener.vi (完整實作)

```labview
┌────────────────────────────────────────────────────────────┐
│            CommandListener.vi (生產環境版)                 │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ Front Panel:                                               │
│   - Stop Button (Boolean)                                  │
│   - Command History (String Array, 最多 100 筆)            │
│   - Status Indicator (LED: Green=執行中, Red=停止)         │
│   - Last Command (String)                                  │
│   - Error Display (Error Cluster)                          │
│                                                            │
│ Block Diagram:                                             │
│                                                            │
│ While Loop (條件: NOT Stop):                               │
│   │                                                        │
│   ├─ WaitForCommand.vi                                    │
│   │   ├─ timeout: 1 (秒,短逾時避免 UI 卡住)               │
│   │   └─ → commandJson, timedOut, error                   │
│   │                                                        │
│   ├─ Case Structure: error.status?                        │
│   │   ├─ False (無錯誤):                                  │
│   │   │   └─ Case Structure: timedOut?                    │
│   │   │       ├─ False (收到指令):                        │
│   │   │       │   ├─ 更新 Last Command                    │
│   │   │       │   ├─ Build Array: 加入 Command History    │
│   │   │       │   ├─ ProcessCommand.vi (處理指令)         │
│   │   │       │   └─ 設定 Status = Green                  │
│   │   │       │                                            │
│   │   │       └─ True (逾時):                             │
│   │   │           └─ 繼續等待 (不顯示訊息)                │
│   │   │                                                    │
│   │   └─ True (有錯誤):                                   │
│   │       ├─ 顯示錯誤訊息                                 │
│   │       ├─ 設定 Status = Red                            │
│   │       └─ Wait 5 秒後重試                              │
│   │                                                        │
│   └─ Wait (ms): 50 (避免 CPU 100%)                        │
│                                                            │
│ Loop End                                                   │
└────────────────────────────────────────────────────────────┘
```

---

## 🚀 效能優化建議

### 1. 重用事件和記憶體 Handle

```labview
優化前 (每次迴圈都建立/關閉):
While Loop {
  CreateEventA → ... → CloseHandle(hEvent)  // 慢!
  CreateFileMappingA → ... → CloseHandle(hMapping)  // 慢!
}

優化後 (迴圈外建立,內部重用):
CreateEventA → hEvent (迴圈前建立一次)
CreateFileMappingA → hMapping (迴圈前建立一次)
  ↓
While Loop {
  WaitForSingleObject(hEvent)  // 快!
  MapViewOfFile(hMapping) → ... → UnmapViewOfFile  // 只映射/解映射
}
  ↓
CloseHandle(hEvent)  (迴圈後關閉)
CloseHandle(hMapping)  (迴圈後關閉)
```

### 2. 使用 Shift Register 保存 Handle

```labview
While Loop [Left Shift Register: hEvent=0, hMapping=0]
  │
  ├─ Case: hEvent == 0?
  │   ├─ True: CreateEventA → hEvent
  │   └─ False: 使用現有 hEvent
  │
  ├─ Case: hMapping == 0?
  │   ├─ True: CreateFileMappingA → hMapping
  │   └─ False: 使用現有 hMapping
  │
  ├─ 主要邏輯 (使用 hEvent, hMapping)
  │
  └─ Right Shift Register: hEvent, hMapping (傳遞到下一次迴圈)
```

---

**完成! 這份文檔提供了完整且正確的 LabVIEW 讀取指令實作。** ✅
