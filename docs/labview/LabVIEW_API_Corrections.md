# LabVIEW 共享記憶體 API 使用修正指南

## ⚠️ 重要修正

### CreateFileMappingA vs OpenFileMappingA 的正確使用

---

## 🔧 兩個 API 的差異

### CreateFileMappingA
- **功能**: 建立**或**開啟共享記憶體區段 (CreateOrOpen 語義)
- **行為**:
  - 如果區段不存在 → 建立新區段
  - 如果區段已存在 → 開啟現有區段
- **錯誤處理**: `GetLastError()` 回傳 `ERROR_ALREADY_EXISTS` (183) 表示已存在

### OpenFileMappingA
- **功能**: 只開啟**現有**的共享記憶體區段
- **行為**:
  - 如果區段存在 → 開啟成功
  - 如果區段不存在 → 回傳 NULL (0),錯誤碼 `ERROR_FILE_NOT_FOUND` (2)
- **使用限制**: 必須確保區段已被其他程序建立

---

## ✅ LabVIEW 的正確使用方式

### 情境 1: LabVIEW 寫入檢驗資料

**區段**: `MES_INSPECTION_DATA`
**正確 API**: `CreateFileMappingA` ✅

**原因**:
- LabVIEW 可能比中介軟體更早啟動
- 使用 `CreateFileMappingA` 確保區段一定可用
- 如果中介軟體已建立,也會成功開啟

```labview
CreateFileMappingA
  ├─ hFile: -1
  ├─ lpAttributes: 0
  ├─ flProtect: 4 (PAGE_READWRITE)
  ├─ dwMaxSizeHigh: 0
  ├─ dwMaxSizeLow: 10485760
  └─ lpName: "MES_INSPECTION_DATA"
  → Output: hMapping (U32)
```

### 情境 2: LabVIEW 讀取設備指令

**區段**: `MES_EQUIPMENT_CMD`
**建議 API**: `CreateFileMappingA` ✅ (最安全)
**替代 API**: `OpenFileMappingA` (如果確定中介軟體已執行)

**最佳實踐 - 使用 CreateFileMappingA**:

```labview
CreateFileMappingA
  ├─ hFile: -1
  ├─ lpAttributes: 0
  ├─ flProtect: 4 (PAGE_READWRITE)
  ├─ dwMaxSizeHigh: 0
  ├─ dwMaxSizeLow: 10485760
  └─ lpName: "MES_EQUIPMENT_CMD"
  → Output: hMapping (U32)
```

**替代方案 - 使用 OpenFileMappingA** (較不安全):

```labview
OpenFileMappingA
  ├─ dwDesiredAccess: 4 (FILE_MAP_READ)
  ├─ bInheritHandle: 0
  └─ lpName: "MES_EQUIPMENT_CMD"
  → Output: hMapping (U32)

# 必須檢查 hMapping == 0 的情況!
Case Structure: hMapping == 0?
  ├─ True: Error "中介軟體服務未執行"
  └─ False: 繼續
```

### 情境 3: LabVIEW 寫入指令確認

**區段**: `MES_EQUIPMENT_CMD_ACK`
**正確 API**: `CreateFileMappingA` ✅

```labview
CreateFileMappingA
  ├─ hFile: -1
  ├─ lpAttributes: 0
  ├─ flProtect: 4 (PAGE_READWRITE)
  ├─ dwMaxSizeHigh: 0
  ├─ dwMaxSizeLow: 10485760
  └─ lpName: "MES_EQUIPMENT_CMD_ACK"
  → Output: hMapping (U32)
```

---

## 📋 完整對照表

| 區段名稱 | 用途 | LabVIEW 角色 | 推薦 API | 替代 API |
|---------|------|------------|---------|---------|
| `MES_INSPECTION_DATA` | 檢驗資料 | 寫入 (Writer) | `CreateFileMappingA` ✅ | - |
| `MES_EQUIPMENT_CMD` | 設備指令 | 讀取 (Reader) | `CreateFileMappingA` ✅ | `OpenFileMappingA` ⚠️ |
| `MES_EQUIPMENT_CMD_ACK` | 指令確認 | 寫入 (Writer) | `CreateFileMappingA` ✅ | - |

**圖例**:
- ✅ 推薦使用 (最安全)
- ⚠️ 可以使用 (需額外錯誤處理)

---

## 🔍 為什麼統一使用 CreateFileMappingA 更好?

### 優點

1. **啟動順序無關**: LabVIEW 和中介軟體任意啟動順序都能正常工作
2. **錯誤處理簡化**: 不需要區分「區段不存在」vs「其他錯誤」
3. **程式碼一致性**: 所有共享記憶體操作使用相同 API
4. **容錯性更高**: 即使中介軟體崩潰重啟,LabVIEW 仍可正常運作

### 缺點

幾乎沒有缺點,唯一的小差異:
- `CreateFileMappingA` 稍微多一點開銷 (檢查是否已存在)
- 但這個開銷可以忽略 (< 1 ms)

---

## 🛠️ 修正後的完整範例

### WriteInspectionData.vi (修正版)

```labview
┌────────────────────────────────────────────────────────────┐
│              WriteInspectionData.vi (正確版本)             │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 1. String to Byte Array (UTF-8)                           │
│ 2. Type Cast: I32 → U8[4]                                 │
│ 3. Build Array: [length] + [data]                         │
│                                                            │
│ 4. CreateFileMappingA ✅ (正確!)                          │
│    ├─ hFile: -1                                           │
│    ├─ lpAttributes: 0                                     │
│    ├─ flProtect: 4                                        │
│    ├─ dwMaxSizeHigh: 0                                    │
│    ├─ dwMaxSizeLow: 10485760                              │
│    └─ lpName: "MES_INSPECTION_DATA"                       │
│                                                            │
│ 5. Case: hMapping == 0? → Error                           │
│ 6. MapViewOfFile (dwDesiredAccess: 2 = WRITE)             │
│ 7. RtlMoveMemory                                          │
│ 8. UnmapViewOfFile                                        │
│ 9. CloseHandle(hMapping)                                  │
│ 10. CreateEventA("MES_DATA_READY") ✅                     │
│ 11. SetEvent                                              │
│ 12. CloseHandle(hEvent)                                   │
└────────────────────────────────────────────────────────────┘
```

### WaitForCommand.vi (修正版)

```labview
┌────────────────────────────────────────────────────────────┐
│              WaitForCommand.vi (修正版本)                  │
├────────────────────────────────────────────────────────────┤
│                                                            │
│ 1. CreateEventA("MES_CMD_READY") ✅ (改用 Create!)        │
│    (或 OpenEventA - 兩者皆可)                             │
│                                                            │
│ 2. WaitForSingleObject(timeout)                           │
│                                                            │
│ 3. Case: waitResult == 0? (WAIT_OBJECT_0)                 │
│                                                            │
│ 4. CreateFileMappingA ✅ (改用 Create,更安全!)            │
│    ├─ hFile: -1                                           │
│    ├─ lpAttributes: 0                                     │
│    ├─ flProtect: 4                                        │
│    ├─ dwMaxSizeHigh: 0                                    │
│    ├─ dwMaxSizeLow: 10485760                              │
│    └─ lpName: "MES_EQUIPMENT_CMD"                         │
│                                                            │
│    【舊版錯誤 ❌】                                         │
│    OpenFileMappingA  ← 如果中介軟體未啟動會失敗!         │
│                                                            │
│ 5. Case: hMapping == 0? → Error                           │
│ 6. MapViewOfFile (dwDesiredAccess: 4 = READ)              │
│ 7. RtlMoveMemory (讀取 4 bytes → length)                  │
│ 8. RtlMoveMemory (讀取 N bytes → data)                    │
│ 9. Byte Array to String (UTF-8)                           │
│ 10. UnmapViewOfFile                                       │
│ 11. CloseHandle(hMapping)                                 │
│ 12. CloseHandle(hEvent)                                   │
└────────────────────────────────────────────────────────────┘
```

---

## 📝 事件 API 的選擇

### 對於事件物件,也是類似的邏輯:

| 用途 | 推薦 API | 替代 API |
|------|---------|---------|
| 建立/觸發事件 | `CreateEventA` ✅ | - |
| 等待事件 | `CreateEventA` ✅ | `OpenEventA` ⚠️ |

**原因**: `CreateEventA` 也有 CreateOrOpen 語義,更安全。

---

## 🔧 實際修改建議

### 如果您已經按照原文檔實作,需要修改的地方:

#### 修改 1: WaitForCommand.vi

**原本 (使用 OpenFileMappingA)**:
```labview
OpenFileMappingA("MES_EQUIPMENT_CMD")
  ↓
Case: hMapping == 0?
  └─ True: Error "中介軟體未執行"
```

**修正為 (使用 CreateFileMappingA)**:
```labview
CreateFileMappingA("MES_EQUIPMENT_CMD", size=10485760)
  ↓
Case: hMapping == 0?
  └─ True: Error "無法建立共享記憶體"
```

#### 修改 2: 事件處理 (可選)

**原本**:
```labview
OpenEventA("MES_CMD_READY")
WaitForSingleObject(...)
```

**建議修正為**:
```labview
CreateEventA("MES_CMD_READY")
WaitForSingleObject(...)
```

---

## ✅ 驗證修正是否成功

### 測試 1: 中介軟體未啟動時

**修正前 (OpenFileMappingA)**:
```
LabVIEW 啟動 → WaitForCommand.vi
  → OpenFileMappingA 回傳 0
  → Error: "找不到共享記憶體" ❌
```

**修正後 (CreateFileMappingA)**:
```
LabVIEW 啟動 → WaitForCommand.vi
  → CreateFileMappingA 成功建立區段
  → WaitForSingleObject 等待事件
  → 中介軟體啟動後會開啟同名區段
  → 正常通訊 ✅
```

### 測試 2: LabVIEW 先啟動

**修正前**:
```
1. LabVIEW 啟動
2. OpenFileMappingA("MES_EQUIPMENT_CMD") → 失敗 ❌
3. 中介軟體啟動
4. LabVIEW 仍無法讀取 (因為第 2 步已失敗)
```

**修正後**:
```
1. LabVIEW 啟動
2. CreateFileMappingA("MES_EQUIPMENT_CMD") → 成功建立 ✅
3. 中介軟體啟動
4. 中介軟體開啟同名區段 (共享同一塊記憶體)
5. 雙方正常通訊 ✅
```

---

## 📚 參考資料

### Microsoft 官方文檔

- [CreateFileMappingA](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createfilemappinga)
  - "If the object exists before the function call, the function returns a handle to the existing object"

- [OpenFileMappingA](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-openfilemappinga)
  - "Opens a named file mapping object"
  - "If the specified name does not exist, the function fails"

---

## 🎯 總結

### 🟢 推薦做法 (最佳實踐)

**LabVIEW 所有共享記憶體操作統一使用 `CreateFileMappingA`**:

```labview
✅ MES_INSPECTION_DATA  → CreateFileMappingA
✅ MES_EQUIPMENT_CMD    → CreateFileMappingA (修正!)
✅ MES_EQUIPMENT_CMD_ACK → CreateFileMappingA
```

**優點**:
- 啟動順序無關
- 錯誤處理簡化
- 程式碼一致性高
- 容錯性強

### 🟡 可接受做法 (需額外處理)

只在確定中介軟體已啟動時使用 `OpenFileMappingA`:

```labview
⚠️ MES_EQUIPMENT_CMD → OpenFileMappingA
   └─ 需檢查中介軟體是否執行中
   └─ 需處理 ERROR_FILE_NOT_FOUND
```

### 🔴 錯誤做法

```labview
❌ 混用 Create/Open 且沒有錯誤處理
❌ 假設中介軟體一定先啟動
❌ 不檢查回傳的 Handle 是否為 0
```

---

**修正完成後,您的 LabVIEW 整合將更加穩定可靠!** 🎉
