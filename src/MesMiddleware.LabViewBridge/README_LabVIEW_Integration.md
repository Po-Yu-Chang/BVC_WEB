# MES Middleware LabVIEW Bridge - 整合指南

## 概述

`MesMiddleware.LabViewBridge.dll` 是一個 .NET Framework 4.0 DLL,專為 LabVIEW 設計,提供與 MES 中介軟體的雙向共享記憶體通訊功能。

### 主要功能

1. **LabVIEW → 中介軟體**:發送檢驗資料 (InspectionRecord)
2. **中介軟體 → LabVIEW**:接收設備指令 (EquipmentCommand)
3. **LabVIEW → 中介軟體**:回傳指令確認 (CommandAcknowledgment)
4. **事件回調機制**:透過 .NET delegate 通知 LabVIEW

---

## 系統需求

- **LabVIEW**: 2012 或更新版本(支援 .NET Framework 4.0)
- **.NET Framework**: 4.0 或更新版本
- **作業系統**: Windows 10/11 或 Windows Server 2019+
- **MES 中介軟體服務**: 必須執行中

---

## DLL 位置

編譯後的 DLL 位於:
```
bin\Release\MesMiddleware.LabViewBridge.dll
```

---

## LabVIEW 整合步驟

### 步驟 1: 新增 .NET 參考

1. 在 LabVIEW Block Diagram 中,右鍵選單 → **.NET** → **Constructor Node**
2. 點擊 **Browse** 按鈕
3. 選擇 `MesMiddleware.LabViewBridge.dll`
4. 選擇類別 `MesMiddleware.LabViewBridge.MesMiddlewareBridge`

### 步驟 2: 初始化 Bridge

```
┌─────────────────────────────────────────┐
│         LabVIEW VI 初始化流程            │
├─────────────────────────────────────────┤
│ 1. [Constructor Node]                   │
│    → 建立 MesMiddlewareBridge 實例      │
│                                          │
│ 2. [Invoke Node: Initialize]            │
│    → 呼叫 Initialize() 方法             │
│    → 輸出: Boolean (Success/Fail)       │
│                                          │
│ 3. [Case Structure] 檢查是否成功         │
│    → True: 繼續下一步                   │
│    → False: 讀取 LastError 屬性顯示錯誤 │
└─────────────────────────────────────────┘
```

**LabVIEW 程式碼範例**:
- Constructor Node → 連接到 MesMiddlewareBridge 參考線
- Invoke Node (Bridge 參考) → 選擇 `Initialize` 方法
- 輸出布林值連接到 Case Structure

### 步驟 3: 註冊回調函數

#### 3.1 註冊檢驗資料回調 (可選)

如果您想在 DLL 監測到 LabVIEW 寫入資料後收到通知:

```
┌──────────────────────────────────────────────┐
│    註冊 InspectionDataReceivedCallback       │
├──────────────────────────────────────────────┤
│ 1. 建立 VI 作為回調處理器                    │
│    → 輸入參數: String (JSON data)           │
│    → 處理邏輯: 記錄日誌、顯示提示等         │
│                                              │
│ 2. [Invoke Node: RegisterInspectionData...] │
│    → 輸入: VI Reference (callback VI)       │
└──────────────────────────────────────────────┘
```

**注意**: 此功能主要用於除錯目的。實際應用中,LabVIEW 自己寫入資料,通常不需要回調。

#### 3.2 註冊設備指令回調 (必要)

接收來自中介軟體的設備指令:

```
┌──────────────────────────────────────────────┐
│   註冊 EquipmentCommandReceivedCallback      │
├──────────────────────────────────────────────┤
│ 1. 建立 VI 處理指令                          │
│    → 輸入參數: String (JSON command)        │
│    → 處理邏輯:                               │
│      - 解析 JSON 取得 CommandType           │
│      - 執行對應操作 (ChangeParameter 等)    │
│      - 產生 CommandAcknowledgment JSON      │
│      - 呼叫 WriteCommandAcknowledgment()    │
│                                              │
│ 2. [Invoke Node: RegisterEquipmentCommand...]│
│    → 輸入: VI Reference (callback VI)       │
└──────────────────────────────────────────────┘
```

### 步驟 4: 啟動監控

```
┌──────────────────────────────────────┐
│      啟動背景監控執行緒               │
├──────────────────────────────────────┤
│ [Invoke Node: StartMonitoring]       │
│ → 輸出: Boolean (Success/Fail)       │
│                                       │
│ 成功後,DLL 會在背景執行緒持續監控:    │
│ - 共享記憶體事件信號                 │
│ - 觸發已註冊的回調函數               │
└──────────────────────────────────────┘
```

### 步驟 5: 發送檢驗資料

```
┌────────────────────────────────────────────────┐
│         發送檢驗資料到中介軟體                  │
├────────────────────────────────────────────────┤
│ 1. 建立 JSON 字串                              │
│    → 使用 LabVIEW JSON Toolkit 或手動拼接     │
│    → 結構參考下方 "JSON 結構範例"             │
│                                                │
│ 2. [Invoke Node: WriteInspectionData]         │
│    → 輸入: String (JSON data)                 │
│    → 輸出: Boolean (Success/Fail)             │
│                                                │
│ 3. 檢查結果                                    │
│    → False: 讀取 LastError 屬性               │
└────────────────────────────────────────────────┘
```

### 步驟 6: 關閉與清理

```
┌──────────────────────────────────┐
│        程式結束時清理資源         │
├──────────────────────────────────┤
│ 1. [Invoke Node: StopMonitoring] │
│    → 停止背景監控執行緒          │
│                                   │
│ 2. [Invoke Node: Dispose]        │
│    → 釋放共享記憶體資源          │
│                                   │
│ 3. [Close Reference]             │
│    → 關閉 .NET 物件參考          │
└──────────────────────────────────┘
```

---

## JSON 結構範例

### 1. InspectionRecord (LabVIEW → 中介軟體)

```json
{
  "RowNo": "ROW_001",
  "ProcName": "盲孔檢驗",
  "DevName": "AOI-MACHINE-01",
  "UserName": "操作員A",
  "WorkClass": "日班",
  "TraceCode": "TRACE123456",
  "LotNo": null,
  "ParamData": [
    {
      "Name": "孔徑_X",
      "Value": "0.25",
      "Unit": "mm",
      "Status": "Pass"
    },
    {
      "Name": "孔徑_Y",
      "Value": "0.26",
      "Unit": "mm",
      "Status": "Pass"
    },
    {
      "Name": "缺陷數量",
      "Value": "0",
      "Unit": "count",
      "Status": "Pass"
    }
  ],
  "Benchmarks": [
    {
      "Name": "孔徑_USL",
      "Value": "0.30",
      "Unit": "mm"
    },
    {
      "Name": "孔徑_LSL",
      "Value": "0.20",
      "Unit": "mm"
    }
  ],
  "OtherData": [
    {
      "Key": "Temperature",
      "Value": "25.5"
    },
    {
      "Key": "Humidity",
      "Value": "60"
    }
  ],
  "InspectionTime": "2025-01-17T12:30:00Z"
}
```

### 2. EquipmentCommand (中介軟體 → LabVIEW)

```json
{
  "CommandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "CommandType": "ChangeParameter",
  "Parameters": {
    "ParameterName": "Threshold",
    "NewValue": "0.5"
  },
  "IssuedAt": "2025-01-17T12:35:00Z"
}
```

**常見 CommandType 值**:
- `ChangeParameter`: 變更參數
- `Calibrate`: 執行校正
- `Start`: 啟動設備
- `Stop`: 停止設備
- `Reset`: 重置設備

### 3. CommandAcknowledgment (LabVIEW → 中介軟體)

```json
{
  "CommandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "Status": "Success",
  "Message": "參數 Threshold 已更新為 0.5",
  "AcknowledgedAt": "2025-01-17T12:35:05Z"
}
```

**常見 Status 值**:
- `Success`: 執行成功
- `Failed`: 執行失敗
- `Timeout`: 執行逾時
- `InvalidParameter`: 參數無效

---

## LabVIEW 實作範例

### 範例 1: 簡單的檢驗資料上傳 VI

```
┌──────────────────────────────────────────────────────┐
│                  InspectionUpload.vi                 │
├──────────────────────────────────────────────────────┤
│ 【前置】                                              │
│ 1. .NET Constructor → MesMiddlewareBridge           │
│ 2. Invoke: Initialize()                             │
│ 3. Case: 檢查初始化結果                             │
│                                                      │
│ 【主迴圈】                                            │
│ While Loop:                                          │
│   ├─ [使用者觸發檢驗]                               │
│   ├─ [取得檢驗資料: 孔徑、缺陷數等]                 │
│   ├─ [建構 JSON 字串]                               │
│   │   └─ 使用 JSONtext Toolkit 或 String Concat    │
│   ├─ [Invoke: WriteInspectionData(jsonString)]     │
│   ├─ [Case: 檢查寫入結果]                           │
│   │   ├─ True → 顯示"上傳成功"                     │
│   │   └─ False → 讀取 LastError,顯示錯誤訊息      │
│   └─ [等待下次檢驗]                                 │
│                                                      │
│ 【清理】                                              │
│ 1. Invoke: Dispose()                                │
│ 2. Close .NET Reference                             │
└──────────────────────────────────────────────────────┘
```

### 範例 2: 接收設備指令 VI

```
┌──────────────────────────────────────────────────────┐
│              CommandReceiver.vi                      │
├──────────────────────────────────────────────────────┤
│ 【前置】                                              │
│ 1. .NET Constructor → MesMiddlewareBridge           │
│ 2. Invoke: Initialize()                             │
│ 3. 建立回調 VI Reference                            │
│    → CommandHandler.vi (見下方)                     │
│ 4. Invoke: RegisterEquipmentCommandCallback(...)    │
│ 5. Invoke: StartMonitoring()                        │
│                                                      │
│ 【主迴圈】                                            │
│ While Loop:                                          │
│   ├─ Property: IsMonitoring (顯示狀態)              │
│   ├─ [等待使用者按下停止按鈕]                       │
│   └─ [背景執行緒自動處理指令回調]                   │
│                                                      │
│ 【清理】                                              │
│ 1. Invoke: StopMonitoring()                         │
│ 2. Invoke: Dispose()                                │
│ 3. Close .NET Reference                             │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│            CommandHandler.vi (回調處理器)            │
├──────────────────────────────────────────────────────┤
│ 【輸入】                                              │
│ - jsonCommand (String): JSON 格式的指令             │
│                                                      │
│ 【處理邏輯】                                          │
│ 1. [Unflatten JSON] → 解析 JSON 字串               │
│ 2. [Get CommandType] → 判斷指令類型                 │
│ 3. [Case Structure] 根據 CommandType:              │
│    ├─ "ChangeParameter"                             │
│    │   ├─ 讀取 Parameters["ParameterName"]         │
│    │   ├─ 讀取 Parameters["NewValue"]              │
│    │   ├─ 執行參數變更 (呼叫設備 API)              │
│    │   └─ 記錄結果                                  │
│    ├─ "Calibrate"                                   │
│    │   └─ 執行校正流程                              │
│    └─ "Start" / "Stop" / "Reset"                    │
│        └─ 執行對應操作                              │
│                                                      │
│ 4. [建構 Acknowledgment JSON]                       │
│    {                                                 │
│      "CommandId": <from input>,                     │
│      "Status": "Success" or "Failed",               │
│      "Message": "執行結果描述",                      │
│      "AcknowledgedAt": <current UTC time>           │
│    }                                                 │
│                                                      │
│ 5. [Invoke: WriteCommandAcknowledgment(ackJson)]   │
│                                                      │
│ 【錯誤處理】                                          │
│ - Try-Catch 包裝所有操作                            │
│ - 錯誤時發送 Status="Failed" 的 Acknowledgment     │
└──────────────────────────────────────────────────────┘
```

---

## LabVIEW JSON 處理建議

### 方法 1: 使用 JSONtext Toolkit (推薦)

安裝 JKI JSON Toolkit (VI Package Manager):
```
JKI JSON - Parse JSON.vi
JKI JSON - Flatten to JSON.vi
```

**優點**: 自動處理資料結構轉換,支援 Cluster/Array

### 方法 2: 手動拼接字串

```labview
【String Concatenation】
"{"
+ "\"RowNo\":\"" + RowNo + "\","
+ "\"ProcName\":\"" + ProcName + "\","
+ "\"DevName\":\"" + DevName + "\","
+ ...
+ "}"
```

**注意**: 需要正確處理轉義字元 (`\"`)

### 方法 3: 使用 LabVIEW 2019+ 原生 JSON VI

```
Flatten to JSON.vi (Data Manipulation palette)
Unflatten from JSON.vi
```

---

## 常見問題排解

### 問題 1: Initialize() 回傳 False

**可能原因**:
- 中介軟體服務未執行
- 共享記憶體區段已被佔用
- 權限不足

**解決方案**:
```labview
Invoke: Initialize()
  ├─ False
  │   └─ Property: LastError
  │       → 顯示錯誤訊息
  │       → 檢查中介軟體服務是否執行
  │       → 以系統管理員身分執行 LabVIEW
```

### 問題 2: WriteInspectionData() 失敗

**可能原因**:
- JSON 格式錯誤
- 資料大小超過 10MB
- 未呼叫 Initialize()

**偵錯步驟**:
1. 驗證 JSON 格式 (使用線上 JSON validator)
2. 檢查資料大小 (`String Length` 函數)
3. 確認 `Initialize()` 已成功執行

### 問題 3: 回調函數未觸發

**檢查項目**:
- [ ] 已呼叫 `RegisterEquipmentCommandCallback()`
- [ ] 已呼叫 `StartMonitoring()`
- [ ] 回調 VI 的連接器窗格格式正確 (一個 String 輸入)
- [ ] 中介軟體確實有發送指令

**偵錯方法**:
```labview
Property: IsMonitoring
  → 應該顯示 True
  → False 表示監控未啟動
```

### 問題 4: 記憶體洩漏

**確保清理流程**:
```labview
【錯誤處理結構】
Flat Sequence:
  Frame 1: Try { 主要邏輯 }
  Frame 2: Finally {
    Invoke: StopMonitoring()
    Invoke: Dispose()
    Close Reference
  }
```

---

## 效能考量

### 資料上傳頻率

- **建議**: 每筆檢驗完成後立即上傳
- **最大頻率**: 約 10 次/秒 (受共享記憶體讀寫速度限制)
- **大量資料**: 考慮批次處理 (Array of InspectionRecord)

### JSON 大小限制

- **單筆上傳**: 最大 10MB (扣除 4 byte 長度前綴)
- **建議大小**: < 1MB 以確保效能

### 執行緒安全

- DLL 內部已處理執行緒同步 (`lock`)
- LabVIEW 可從多個 VI 同時呼叫 (自動序列化)

---

## 進階功能

### 自訂共享記憶體區段名稱

目前版本使用固定名稱:
- `MES_INSPECTION_DATA` (檢驗資料)
- `MES_EQUIPMENT_CMD` (設備指令)
- `MES_EQUIPMENT_CMD_ACK` (指令確認)

如需自訂,請修改 `MesMiddlewareBridge.cs` 的常數並重新編譯。

### COM 註冊 (可選)

如果需要 LabVIEW 透過 COM 註冊使用:

```powershell
# 以系統管理員身分執行
regasm /codebase MesMiddleware.LabViewBridge.dll
```

**注意**: 當前版本已移除自動 COM 註冊,LabVIEW 可直接載入 DLL。

---

## 版本資訊

- **DLL 版本**: 1.0.0.0
- **目標框架**: .NET Framework 4.0
- **LabVIEW 相容性**: 2012 或更新版本
- **最後更新**: 2025-01-17

---

## 技術支援

如有問題,請查看:
1. **日誌檔案**: LabVIEW 執行目錄
2. **中介軟體日誌**: `logs/middleware-service-{Date}.log`
3. **GitHub Issues**: [專案 Issues 頁面](https://github.com/your-repo/issues)

---

## 附錄: API 參考

### MesMiddlewareBridge 類別

| 方法/屬性 | 類型 | 說明 |
|-----------|------|------|
| `Initialize()` | 方法 | 初始化共享記憶體,回傳 bool |
| `RegisterInspectionDataCallback(callback)` | 方法 | 註冊檢驗資料回調 |
| `RegisterEquipmentCommandCallback(callback)` | 方法 | 註冊設備指令回調 |
| `StartMonitoring()` | 方法 | 啟動背景監控,回傳 bool |
| `StopMonitoring()` | 方法 | 停止背景監控 |
| `WriteInspectionData(json)` | 方法 | 寫入檢驗資料,回傳 bool |
| `WriteCommandAcknowledgment(json)` | 方法 | 寫入指令確認,回傳 bool |
| `Dispose()` | 方法 | 釋放資源 |
| `IsMonitoring` | 屬性 | 取得監控狀態 (bool) |
| `LastError` | 屬性 | 取得最後錯誤訊息 (string) |

### Callback 委派

| 委派名稱 | 簽章 | 說明 |
|----------|------|------|
| `InspectionDataReceivedCallback` | `void(string jsonData)` | 檢驗資料回調 |
| `EquipmentCommandReceivedCallback` | `void(string jsonCommand)` | 設備指令回調 |

---

**祝您整合順利!**
