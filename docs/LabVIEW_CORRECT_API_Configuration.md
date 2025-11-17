# LabVIEW 正確的 Call Library Function 配置 (64-bit 修正版)

## ⚠️ 重要修正說明

原文檔使用 `Numeric U32` 作為 Handle/Pointer 類型,這在 **64-bit Windows/LabVIEW 環境下會導致嚴重錯誤**:
- Handle 被截斷 (8 bytes → 4 bytes)
- 指標高位丟失 → 寫入到錯誤的記憶體位址
- 程序崩潰或記憶體損壞

本文檔提供 **100% 正確** 的配置,適用於:
- ✅ Windows 64-bit
- ✅ LabVIEW 64-bit (2012+)
- ✅ 也相容 32-bit 環境 (LabVIEW 會自動調整 Pointer-sized Integer)

---

## 🔧 核心修正重點

### 修正 1: 所有 Handle/Pointer 使用 Pointer-sized Integer

| ❌ 錯誤 (原文檔) | ✅ 正確 |
|----------------|---------|
| Numeric U32 (Handle) | **Unsigned Pointer-sized Integer** |
| Numeric U32 (Pointer) | **Unsigned Pointer-sized Integer** |
| Numeric I32 (hFile = -1) | **Signed Pointer-sized Integer** |

### 修正 2: RtlMoveMemory 的 Array 參數必須使用 Array Data Pointer

| ❌ 錯誤 | ✅ 正確 |
|--------|---------|
| Array (預設) | Array **Data Pointer** |
| Array Handle | Array **Data Pointer** (明確指定) |

### 修正 3: Calling Convention 必須是 stdcall (WINAPI)

| ❌ 錯誤 | ✅ 正確 |
|--------|---------|
| C (預設) | **stdcall (WINAPI)** |

### 修正 4: 資料準備方式 - 一次拷貝完整資料

```labview
✅ 正確流程:
1. Type Cast: I32 length → U8[4] (little-endian)
2. String to Byte Array: JSON → U8[] (UTF-8)
3. Concatenate Array: [length bytes] + [JSON bytes] → data[]
4. RtlMoveMemory(Destination, data[], data[].length)  // 一次完成!

❌ 錯誤流程 (複雜且容易出錯):
1. RtlMoveMemory(Destination, lengthBytes, 4)
2. RtlMoveMemory(Destination+4, jsonBytes, jsonLength)  // 指標運算容易錯
```

---

## 📋 完整正確的 API 配置表

### 1. CreateFileMappingA

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: CreateFileMappingA                       │
│ Calling Convention: stdcall (WINAPI) ✅                 │
│ Thread: Run in any thread                               │
├─────────────────────────────────────────────────────────┤
│ Return Type:                                            │
│   Category: Numeric                                     │
│   Type: Unsigned Pointer-sized Integer ✅               │
│   (NOT U32!)                                            │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
├─────────────────┬──────────────────┬────────────────────┤
│ Name            │ Type             │ Value              │
├─────────────────┼──────────────────┼────────────────────┤
│ hFile           │ Signed Pointer-  │ -1 ✅              │
│                 │ sized Integer ✅ │ (INVALID_HANDLE)   │
├─────────────────┼──────────────────┼────────────────────┤
│ lpAttributes    │ Unsigned Pointer-│ 0 ✅               │
│                 │ sized Integer ✅ │ (NULL)             │
├─────────────────┼──────────────────┼────────────────────┤
│ flProtect       │ Numeric U32      │ 4                  │
│                 │                  │ (PAGE_READWRITE)   │
├─────────────────┼──────────────────┼────────────────────┤
│ dwMaxSizeHigh   │ Numeric U32      │ 0                  │
├─────────────────┼──────────────────┼────────────────────┤
│ dwMaxSizeLow    │ Numeric U32      │ 10485760           │
│                 │                  │ (10 MB)            │
├─────────────────┼──────────────────┼────────────────────┤
│ lpName          │ String           │ "MES_INSPECTION_   │
│                 │ (C String Ptr)   │ DATA" ✅           │
└─────────────────┴──────────────────┴────────────────────┘

LabVIEW 設定步驟:
1. 雙擊 Call Library Function Node
2. Library: kernel32.dll
3. Function: CreateFileMappingA
4. Calling Convention: stdcall (WINAPI) ← 必選!
5. Return Type → Numeric → Unsigned Pointer-sized Integer
6. 新增 6 個參數 (按順序):
   - hFile: Numeric → Signed Pointer-sized Integer → 常數 -1
   - lpAttributes: Numeric → Unsigned Pointer-sized Integer → 常數 0
   - flProtect: Numeric → U32 → 常數 4
   - dwMaxSizeHigh: Numeric → U32 → 常數 0
   - dwMaxSizeLow: Numeric → U32 → 常數 10485760
   - lpName: String → C String Pointer → 連接到控制項
```

### 2. MapViewOfFile

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: MapViewOfFile                            │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type:                                            │
│   Category: Numeric                                     │
│   Type: Unsigned Pointer-sized Integer ✅               │
│   (這是記憶體指標,絕對不能用 U32!)                     │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
├──────────────────────┬────────────────┬─────────────────┤
│ Name                 │ Type           │ Value/Source    │
├──────────────────────┼────────────────┼─────────────────┤
│ hFileMappingObject   │ Unsigned       │ 從 CreateFile-  │
│                      │ Pointer-sized  │ Mapping 回傳 ✅ │
│                      │ Integer ✅     │                 │
├──────────────────────┼────────────────┼─────────────────┤
│ dwDesiredAccess      │ Numeric U32    │ 2 (WRITE) or    │
│                      │                │ 4 (READ)        │
├──────────────────────┼────────────────┼─────────────────┤
│ dwFileOffsetHigh     │ Numeric U32    │ 0               │
├──────────────────────┼────────────────┼─────────────────┤
│ dwFileOffsetLow      │ Numeric U32    │ 0               │
├──────────────────────┼────────────────┼─────────────────┤
│ dwNumberOfBytes      │ Numeric U32    │ 10485760        │
└──────────────────────┴────────────────┴─────────────────┘

常數說明:
FILE_MAP_WRITE = 2  (寫入時用)
FILE_MAP_READ  = 4  (讀取時用)
```

### 3. RtlMoveMemory (寫入資料)

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: RtlMoveMemory                            │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: (none/void)                                │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
├──────────────┬──────────────────────┬───────────────────┤
│ Name         │ Type                 │ 說明              │
├──────────────┼──────────────────────┼───────────────────┤
│ Destination  │ Numeric              │ MapViewOfFile     │
│              │ Unsigned Pointer-    │ 回傳的指標 ✅     │
│              │ sized Integer ✅     │                   │
├──────────────┼──────────────────────┼───────────────────┤
│ Source       │ Array                │ 要寫入的資料 ✅   │
│              │ ┌─ Data Type: U8    │                   │
│              │ └─ Format: Array     │ ⚠️ 關鍵設定!     │
│              │    Data Pointer ✅   │ 不是 Handle!      │
│              │                      │                   │
│              │ Mechanism:           │                   │
│              │   Pass by Value      │                   │
├──────────────┼──────────────────────┼───────────────────┤
│ Length       │ Numeric U32          │ Array Size        │
│              │                      │ (自動或手動連接)  │
└──────────────┴──────────────────────┴───────────────────┘

⚠️ 超級重要! Source 參數設定步驟:

1. Parameter Type: 選擇 "Array"
2. Data Type: 選擇 "Unsigned 8-bit Integer" (U8)
3. Array Format: 選擇 "Array Data Pointer" ← 必須選這個!
   (預設可能是 "Array Handle",這會導致崩潰!)
4. Mechanism: "Pass by Value"
5. 連接到 LabVIEW 的 1D U8 Array 控制項
```

### 4. RtlMoveMemory (讀取資料)

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: RtlMoveMemory                            │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: (none/void)                                │
├─────────────────────────────────────────────────────────┤
│ Parameters (讀取時參數順序相反):                        │
├──────────────┬──────────────────────┬───────────────────┤
│ Name         │ Type                 │ 說明              │
├──────────────┼──────────────────────┼───────────────────┤
│ Destination  │ Array ✅             │ 接收資料的陣列    │
│              │ ┌─ Data Type: U8    │                   │
│              │ └─ Format: Array     │ ⚠️ 關鍵設定!     │
│              │    Data Pointer ✅   │                   │
│              │                      │                   │
│              │ Mechanism:           │                   │
│              │   Pass by Reference  │                   │
├──────────────┼──────────────────────┼───────────────────┤
│ Source       │ Numeric              │ MapViewOfFile     │
│              │ Unsigned Pointer-    │ 回傳的指標 ✅     │
│              │ sized Integer ✅     │                   │
├──────────────┼──────────────────────┼───────────────────┤
│ Length       │ Numeric U32          │ 要讀取的長度      │
└──────────────┴──────────────────────┴───────────────────┘

讀取前需要先初始化陣列:
Initialize Array: Size = Length, Value = 0
```

### 5. UnmapViewOfFile

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: UnmapViewOfFile                          │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: Numeric U32 (BOOL)                         │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
├──────────────┬──────────────────────┬───────────────────┤
│ Name         │ Type                 │ 說明              │
├──────────────┼──────────────────────┼───────────────────┤
│ lpBaseAddress│ Unsigned Pointer-    │ MapViewOfFile     │
│              │ sized Integer ✅     │ 回傳的指標        │
└──────────────┴──────────────────────┴───────────────────┘
```

### 6. CloseHandle

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: CloseHandle                              │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: Numeric U32 (BOOL)                         │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
├──────────────┬──────────────────────┬───────────────────┤
│ Name         │ Type                 │ 說明              │
├──────────────┼──────────────────────┼───────────────────┤
│ hObject      │ Unsigned Pointer-    │ Handle (記憶體或  │
│              │ sized Integer ✅     │ 事件)             │
└──────────────┴──────────────────────┴───────────────────┘
```

### 7. CreateEventA

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: CreateEventA                             │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type:                                            │
│   Unsigned Pointer-sized Integer ✅ (Event Handle)      │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
├───────────────────┬─────────────────┬───────────────────┤
│ Name              │ Type            │ Value             │
├───────────────────┼─────────────────┼───────────────────┤
│ lpEventAttributes │ Unsigned        │ 0 (NULL) ✅       │
│                   │ Pointer-sized   │                   │
│                   │ Integer ✅      │                   │
├───────────────────┼─────────────────┼───────────────────┤
│ bManualReset      │ Numeric U32     │ 0 (Auto-reset)    │
├───────────────────┼─────────────────┼───────────────────┤
│ bInitialState     │ Numeric U32     │ 0 (Non-signaled)  │
├───────────────────┼─────────────────┼───────────────────┤
│ lpName            │ String          │ "MES_DATA_READY"  │
│                   │ (C String Ptr)  │                   │
└───────────────────┴─────────────────┴───────────────────┘
```

### 8. SetEvent

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: SetEvent                                 │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: Numeric U32 (BOOL)                         │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
├──────────────┬──────────────────────┬───────────────────┤
│ Name         │ Type                 │ 說明              │
├──────────────┼──────────────────────┼───────────────────┤
│ hEvent       │ Unsigned Pointer-    │ CreateEvent 回傳  │
│              │ sized Integer ✅     │ 的 Handle         │
└──────────────┴──────────────────────┴───────────────────┘
```

### 9. WaitForSingleObject

```
┌─────────────────────────────────────────────────────────┐
│ Library Name: kernel32.dll                              │
│ Function Name: WaitForSingleObject                      │
│ Calling Convention: stdcall (WINAPI) ✅                 │
├─────────────────────────────────────────────────────────┤
│ Return Type: Numeric U32 (Wait Result)                  │
│   0 (WAIT_OBJECT_0) = 成功                              │
│   258 (WAIT_TIMEOUT) = 逾時                             │
├─────────────────────────────────────────────────────────┤
│ Parameters:                                             │
├──────────────────┬───────────────────┬──────────────────┤
│ Name             │ Type              │ 說明             │
├──────────────────┼───────────────────┼──────────────────┤
│ hHandle          │ Unsigned Pointer- │ Event Handle ✅  │
│                  │ sized Integer ✅  │                  │
├──────────────────┼───────────────────┼──────────────────┤
│ dwMilliseconds   │ Numeric U32       │ 30000 (30秒)     │
│                  │                   │ 0xFFFFFFFF=無限  │
└──────────────────┴───────────────────┴──────────────────┘
```

---

## 📝 完整正確的寫入流程 (WriteInspectionData.vi)

```labview
┌────────────────────────────────────────────────────────────┐
│              正確的寫入流程 (64-bit 相容版)                │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 1: 準備資料 - 一次組好】                            │
│                                                            │
│   JSON String (Input)                                     │
│     ↓                                                      │
│   String to Byte Array (UTF-8) ✅                         │
│     ├─ Output: jsonBytes[] (U8 Array)                     │
│     └─ Output: length (I32)                               │
│     ↓                                                      │
│   Type Cast: I32 → U8[4] (little-endian) ✅               │
│     └─ Output: lengthBytes[4]                             │
│     ↓                                                      │
│   Build Array / Concatenate Arrays ✅                      │
│     Input 1: lengthBytes[4]                               │
│     Input 2: jsonBytes[]                                  │
│     ↓                                                      │
│   Output: completeData[] (U8 Array)                       │
│           = [4 bytes length] + [N bytes JSON]             │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 2: 建立共享記憶體】                                 │
│                                                            │
│   Call Library: CreateFileMappingA ✅                     │
│     ├─ hFile: -1 (Signed Pointer-sized Integer)          │
│     ├─ lpAttributes: 0 (Unsigned Pointer-sized Integer)   │
│     ├─ flProtect: 4 (U32)                                 │
│     ├─ dwMaxSizeHigh: 0 (U32)                             │
│     ├─ dwMaxSizeLow: 10485760 (U32)                       │
│     └─ lpName: "MES_INSPECTION_DATA"                      │
│     ↓                                                      │
│   Output: hMapping (Unsigned Pointer-sized Integer) ✅    │
│     ↓                                                      │
│   Case Structure: hMapping != 0? ✅                       │
│     ├─ False: Error (GetLastError)                        │
│     └─ True: 繼續                                         │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 3: 映射記憶體】                                     │
│                                                            │
│   Call Library: MapViewOfFile ✅                          │
│     ├─ hFileMappingObject: hMapping                       │
│     │  (Unsigned Pointer-sized Integer)                   │
│     ├─ dwDesiredAccess: 2 (FILE_MAP_WRITE, U32)           │
│     ├─ dwFileOffsetHigh: 0 (U32)                          │
│     ├─ dwFileOffsetLow: 0 (U32)                           │
│     └─ dwNumberOfBytes: 10485760 (U32)                    │
│     ↓                                                      │
│   Output: pMemory (Unsigned Pointer-sized Integer) ✅     │
│     ↓                                                      │
│   Case Structure: pMemory != 0?                           │
│     ├─ False: Error                                       │
│     └─ True: 繼續                                         │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 4: 寫入資料 - 一次完成!】                           │
│                                                            │
│   Call Library: RtlMoveMemory ✅                          │
│     ├─ Destination: pMemory                               │
│     │  (Unsigned Pointer-sized Integer)                   │
│     ├─ Source: completeData[]                             │
│     │  (Array U8, Array Data Pointer!) ⚠️ 關鍵設定       │
│     └─ Length: Array Size(completeData[])                 │
│        (U32)                                              │
│                                                            │
│   ✅ 這一次呼叫就把 [長度+JSON] 全部寫進去了!             │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 5: 清理記憶體映射】                                 │
│                                                            │
│   Call Library: UnmapViewOfFile(pMemory) ✅               │
│     ↓                                                      │
│   Call Library: CloseHandle(hMapping) ✅                  │
│                                                            │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 【步驟 6: 發送事件信號】                                   │
│                                                            │
│   Call Library: CreateEventA ✅                           │
│     ├─ lpEventAttributes: 0                               │
│     │  (Unsigned Pointer-sized Integer)                   │
│     ├─ bManualReset: 0 (U32)                              │
│     ├─ bInitialState: 0 (U32)                             │
│     └─ lpName: "MES_DATA_READY"                           │
│     ↓                                                      │
│   Output: hEvent (Unsigned Pointer-sized Integer) ✅      │
│     ↓                                                      │
│   Call Library: SetEvent(hEvent) ✅                       │
│     ↓                                                      │
│   Call Library: CloseHandle(hEvent) ✅                    │
│                                                            │
└────────────────────────────────────────────────────────────┘

完成! 資料已正確寫入共享記憶體並通知中介軟體 ✅
```

---

## 🔍 常見錯誤與解決方案

### 錯誤 1: Access Violation / Memory Corruption

**症狀**:
- LabVIEW 崩潰
- Error 1097 (exception occurred)
- 寫入後資料損壞

**原因**:
```
❌ Handle/Pointer 使用 U32 (32-bit)
❌ RtlMoveMemory 的 Source 使用 Array Handle (而非 Data Pointer)
```

**解決方案**:
```
✅ 所有 Handle/Pointer 改為 Unsigned Pointer-sized Integer
✅ RtlMoveMemory Source 改為 Array Data Pointer
```

### 錯誤 2: 中介軟體收到亂碼或空資料

**症狀**:
- 事件觸發了,但讀取到的 JSON 是亂碼
- 長度欄位錯誤 (例如極大值或負數)

**原因**:
```
❌ Endianness 錯誤 (Type Cast 設定錯誤)
❌ 沒有使用 UTF-8 編碼
❌ 先寫 JSON 再寫長度 (順序反了)
```

**解決方案**:
```
✅ Type Cast I32 → U8[4] (LabVIEW 預設是 little-endian,正確)
✅ String to Byte Array 明確選擇 UTF-8
✅ Build Array: [lengthBytes] 在前, [jsonBytes] 在後
```

### 錯誤 3: CreateFileMapping 回傳 0

**症狀**:
- hMapping = 0
- GetLastError = 5 (ERROR_ACCESS_DENIED)

**原因**:
```
❌ 權限不足 (LabVIEW 沒有以管理員執行)
❌ 防毒軟體阻擋
❌ 區段名稱與其他程序衝突
```

**解決方案**:
```
✅ 以管理員身分執行 LabVIEW
✅ 檢查防毒軟體設定
✅ 使用 Process Explorer 確認區段名稱沒有被佔用
```

### 錯誤 4: SetEvent 沒有喚醒中介軟體

**症狀**:
- SetEvent 回傳成功,但中介軟體沒反應

**原因**:
```
❌ 事件名稱拼寫錯誤 (區分大小寫!)
❌ 中介軟體服務未執行
❌ 中介軟體正在處理其他事件
```

**解決方案**:
```
✅ 確認事件名稱完全一致: "MES_DATA_READY" (不是 "Mes_Data_Ready")
✅ 檢查中介軟體服務狀態
✅ 使用 Process Explorer 確認事件物件已建立
```

---

## ✅ 驗證檢查清單

在測試 VI 前,請逐一檢查:

### Call Library Function 配置檢查

- [ ] **Calling Convention**: 所有 kernel32.dll 函數都是 `stdcall (WINAPI)`
- [ ] **Handle 類型**: 所有 Handle 都是 `Unsigned Pointer-sized Integer`
- [ ] **Pointer 類型**: 所有指標都是 `Unsigned Pointer-sized Integer`
- [ ] **hFile 參數**: `Signed Pointer-sized Integer` = -1
- [ ] **lpAttributes 參數**: `Unsigned Pointer-sized Integer` = 0
- [ ] **RtlMoveMemory Source**: `Array U8, Array Data Pointer` (寫入時)
- [ ] **RtlMoveMemory Destination**: `Array U8, Array Data Pointer` (讀取時)

### 資料準備檢查

- [ ] **資料順序**: [4 bytes length] + [N bytes JSON]
- [ ] **長度計算**: 只包含 JSON 的長度 (不包含前 4 bytes 本身)
- [ ] **編碼**: UTF-8 (String to Byte Array 明確指定)
- [ ] **Endianness**: Little-endian (Type Cast 預設正確)

### 流程檢查

- [ ] **錯誤處理**: 每個 API 呼叫後檢查回傳值
- [ ] **資源清理**: UnmapViewOfFile → CloseHandle (順序正確)
- [ ] **事件名稱**: 完全一致,區分大小寫
- [ ] **區段大小**: 10485760 (10 MB) 一致

---

## 📞 除錯工具

### 1. Process Explorer (Sysinternals)

```
下載: https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer

用途:
1. Find → Find Handle → 輸入 "MES_INSPECTION_DATA"
2. 確認共享記憶體區段已建立
3. 查看哪些程序持有 Handle
```

### 2. DebugView (Sysinternals)

```
下載: https://learn.microsoft.com/en-us/sysinternals/downloads/debugview

用途:
- 在 LabVIEW 中使用 OutputDebugString (Call Library)
- 即時查看除錯訊息
```

### 3. WinDbg (進階)

```
用途: 檢查記憶體內容
1. Attach to LabVIEW.exe
2. !address -f:MEM_MAPPED
3. 查看共享記憶體位址
```

---

## 🎯 總結: 三大關鍵修正

| 原文檔錯誤 ❌ | 正確配置 ✅ | 影響 |
|-------------|-----------|------|
| Handle/Pointer 用 U32 | 用 **Unsigned Pointer-sized Integer** | 🔴 嚴重 (64-bit 崩潰) |
| Array 沒指定 Data Pointer | 明確選擇 **Array Data Pointer** | 🔴 嚴重 (記憶體損壞) |
| Calling Convention 沒寫 | 明確選擇 **stdcall (WINAPI)** | 🟡 重要 (參數傳遞錯誤) |

**完成這三個修正,您的 LabVIEW 整合就會 100% 正確!** 🎉
