# MesMiddleware.LabViewBridge

**LabVIEW 專用 MES 中介軟體通訊 DLL (.NET Framework 4.0)**

---

## 📦 專案概述

`MesMiddleware.LabViewBridge.dll` 是一個專為 LabVIEW 設計的 .NET Framework 4.0 類別庫,提供與 MES 中介軟體的雙向共享記憶體通訊功能。

### 核心功能

✅ **雙向通訊**
- **LabVIEW → 中介軟體**: 發送檢驗資料 (InspectionRecord)
- **中介軟體 → LabVIEW**: 接收設備指令 (EquipmentCommand)
- **LabVIEW → 中介軟體**: 回傳指令確認 (CommandAcknowledgment)

✅ **事件驅動架構**
- .NET Delegate 回調機制
- 背景執行緒監控
- 自動事件通知

✅ **COM 互操作性**
- LabVIEW 2012+ 完全相容
- COM Visible 介面
- 自動記憶體管理

---

## 🚀 快速開始

### 1. 編譯 DLL

```bash
cd src/MesMiddleware.LabViewBridge
dotnet build -c Release
```

**輸出位置**: `bin\Release\MesMiddleware.LabViewBridge.dll`

### 2. LabVIEW 整合基本步驟

#### Step 1: 載入 .NET Assembly

在 LabVIEW Block Diagram:
1. 右鍵 → `.NET` → `Constructor Node`
2. 瀏覽選擇 `MesMiddleware.LabViewBridge.dll`
3. 選擇類別 `MesMiddlewareBridge`

#### Step 2: 初始化

```
.NET Constructor
  ↓
Invoke Node: Initialize()
  ↓ (Boolean)
Case Structure
  ├─ True: 繼續
  └─ False: 讀取 LastError 顯示錯誤
```

#### Step 3: 發送檢驗資料

```labview
JSON String (建構檢驗資料 JSON)
  ↓
Invoke Node: WriteInspectionData(jsonString)
  ↓ (Boolean)
顯示上傳結果
```

#### Step 4: 接收設備指令

```labview
建立回調 VI
  ↓
Invoke Node: RegisterEquipmentCommandCallback(callbackVI)
  ↓
Invoke Node: StartMonitoring()
  ↓
等待指令 (背景自動處理)
```

---

## 📚 文件資源

### 詳細整合指南
👉 **[README_LabVIEW_Integration.md](README_LabVIEW_Integration.md)**
- 完整 LabVIEW 整合步驟
- JSON 結構範例
- VI 設計模式
- 常見問題排解

### C# 使用範例
👉 **[EXAMPLE_CSharp_Usage.cs](EXAMPLE_CSharp_Usage.cs)**
- 3 個完整範例程式
- 模擬 LabVIEW 行為
- 可直接執行測試

---

## 🏗️ 專案結構

```
MesMiddleware.LabViewBridge/
├── Models/                          # 資料模型
│   ├── InspectionRecord.cs          # 檢驗記錄
│   ├── ParamDataItem.cs             # 參數資料項
│   ├── BenchmarkItem.cs             # 基準值
│   ├── OtherDataItem.cs             # 其他元資料
│   ├── EquipmentCommand.cs          # 設備指令
│   └── CommandAcknowledgment.cs     # 指令確認
├── SharedMemoryManager.cs           # 共享記憶體管理 (內部類別)
├── MesMiddlewareBridge.cs           # 主要 API 類別 (COM Visible)
├── Properties/
│   └── AssemblyInfo.cs              # 組件資訊
├── README.md                        # 本檔案
├── README_LabVIEW_Integration.md    # LabVIEW 整合指南
└── EXAMPLE_CSharp_Usage.cs          # C# 範例程式
```

---

## 🔧 API 參考

### MesMiddlewareBridge 類別

#### 方法

| 方法 | 回傳類型 | 說明 |
|------|----------|------|
| `Initialize()` | `bool` | 初始化共享記憶體 |
| `RegisterInspectionDataCallback(callback)` | `void` | 註冊檢驗資料回調 |
| `RegisterEquipmentCommandCallback(callback)` | `void` | 註冊設備指令回調 |
| `StartMonitoring()` | `bool` | 啟動背景監控 |
| `StopMonitoring()` | `void` | 停止背景監控 |
| `WriteInspectionData(json)` | `bool` | 寫入檢驗資料 |
| `WriteCommandAcknowledgment(json)` | `bool` | 寫入指令確認 |
| `Dispose()` | `void` | 釋放資源 |

#### 屬性

| 屬性 | 類型 | 說明 |
|------|------|------|
| `IsMonitoring` | `bool` | 取得監控狀態 |
| `LastError` | `string` | 取得最後錯誤訊息 |

#### 回調委派

```csharp
// 檢驗資料回調
public delegate void InspectionDataReceivedCallback(string jsonData);

// 設備指令回調
public delegate void EquipmentCommandReceivedCallback(string jsonCommand);
```

---

## 📋 JSON 結構範例

### InspectionRecord (檢驗資料)

```json
{
  "RowNo": "ROW_001",
  "ProcName": "盲孔檢驗",
  "DevName": "AOI-MACHINE-01",
  "UserName": "操作員A",
  "WorkClass": "日班",
  "TraceCode": "TRACE123456",
  "ParamData": [
    {
      "Name": "孔徑_X",
      "Value": "0.25",
      "Unit": "mm",
      "Status": "Pass"
    }
  ],
  "Benchmarks": [
    {
      "Name": "孔徑_USL",
      "Value": "0.30",
      "Unit": "mm"
    }
  ],
  "OtherData": [
    {
      "Key": "Temperature",
      "Value": "25.5"
    }
  ],
  "InspectionTime": "2025-01-17T12:30:00Z"
}
```

### EquipmentCommand (設備指令)

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

### CommandAcknowledgment (指令確認)

```json
{
  "CommandId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "Status": "Success",
  "Message": "參數 Threshold 已更新為 0.5",
  "AcknowledgedAt": "2025-01-17T12:35:05Z"
}
```

---

## ⚙️ 共享記憶體規格

### 記憶體區段

| 區段名稱 | 大小 | 用途 | 事件信號 |
|----------|------|------|----------|
| `MES_INSPECTION_DATA` | 10 MB | 檢驗資料 (LabVIEW → 中介軟體) | `MES_DATA_READY` |
| `MES_EQUIPMENT_CMD` | 10 MB | 設備指令 (中介軟體 → LabVIEW) | `MES_CMD_READY` |
| `MES_EQUIPMENT_CMD_ACK` | 10 MB | 指令確認 (LabVIEW → 中介軟體) | `MES_ACK_READY` |

### 資料格式

```
┌──────────────────────────────────────────────────┐
│ Byte 0-3:   資料長度 (int32, little-endian)      │
├──────────────────────────────────────────────────┤
│ Byte 4-N:   UTF-8 JSON 字串                      │
└──────────────────────────────────────────────────┘
```

---

## 🛠️ 系統需求

- **LabVIEW**: 2012 或更新版本
- **.NET Framework**: 4.0 或更新版本
- **作業系統**: Windows 10/11 或 Windows Server 2019+
- **中介軟體**: MesMiddleware 服務必須執行中

---

## 🔍 故障排除

### 問題 1: Initialize() 回傳 False

**檢查項目**:
- [ ] MES 中介軟體服務是否執行中?
- [ ] LabVIEW 是否以足夠權限執行?
- [ ] 讀取 `LastError` 屬性查看詳細錯誤

### 問題 2: WriteInspectionData() 失敗

**檢查項目**:
- [ ] JSON 格式是否正確? (使用線上 validator 驗證)
- [ ] 資料大小是否超過 10MB?
- [ ] 是否已成功呼叫 `Initialize()`?

### 問題 3: 回調函數未觸發

**檢查項目**:
- [ ] 是否已呼叫 `RegisterEquipmentCommandCallback()`?
- [ ] 是否已呼叫 `StartMonitoring()`?
- [ ] 回調 VI 的連接器窗格是否正確? (一個 String 輸入)
- [ ] 中介軟體是否確實有發送指令?

---

## 📈 效能指標

| 指標 | 數值 |
|------|------|
| 最大資料大小 | 10 MB (單筆) |
| 建議資料大小 | < 1 MB |
| 上傳頻率 | 最大 ~10 次/秒 |
| 回調延遲 | < 100 ms |

---

## 🔐 執行緒安全

- ✅ DLL 內部使用 `lock` 確保執行緒安全
- ✅ 可從多個 LabVIEW VI 同時呼叫
- ✅ 回調函數在背景執行緒執行 (注意 UI 更新)

---

## 📄 授權條款

本專案採用 MIT 授權條款。

---

## 🙏 技術支援

如有問題,請參考:
1. **詳細整合指南**: [README_LabVIEW_Integration.md](README_LabVIEW_Integration.md)
2. **C# 範例程式**: [EXAMPLE_CSharp_Usage.cs](EXAMPLE_CSharp_Usage.cs)
3. **主專案 README**: [../../README.md](../../README.md)
4. **GitHub Issues**: [建立 Issue](https://github.com/your-repo/issues)

---

## 📝 版本資訊

- **版本**: 1.0.0.0
- **目標框架**: .NET Framework 4.0
- **LabVIEW 相容性**: 2012 或更新版本
- **最後更新**: 2025-01-17

---

**祝您整合順利!** 🚀
