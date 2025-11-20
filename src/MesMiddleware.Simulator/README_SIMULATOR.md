# MES Cloud Simulator 使用指南

## 🚀 快速開始

### 1. 啟動模擬器

```bash
cd src/MesMiddleware.Simulator
dotnet run
```

### 2. 啟動服務器

1. 點擊 **🚀 啟動服務器** 按鈕
2. 伺服器將監聽 `http://localhost:5200`
3. Swagger UI: `http://localhost:5200/swagger`

### 3. 生成測試 Token

1. 啟動服務器後,點擊 **🔄 生成新 Token** 按鈕
2. 系統會自動生成測試設備 (TEST-MACHINE-001) 並分配 Token
3. Token 會自動複製到剪貼簿
4. 也可以點擊 **📋 複製 Token** 再次複製

**測試設備資訊**:
- 機台編號: `TEST-MACHINE-001`
- IP 地址: `192.168.1.100`
- Token 格式: `autoprt<32位GUID>` (例: `autoprt7d4e2f8a9b1c3d5e6f7a8b9c0d1e2f3a`)

---

## 📋 API 端點

### 1. 設備登入

**POST** `/CimforceTraceMgrDev/api/prtmac/prtmacuserlogin`

**請求 Body**:
```json
{
  "prtMacNo": "TEST-MACHINE-001"
}
```

**響應**:
```json
{
  "success": true,
  "data": {
    "prtMacNo": "TEST-MACHINE-001",
    "ipAddr": "192.168.1.100",
    "token": "autoprt7d4e2f8a9b1c3d5e6f7a8b9c0d1e2f3a",
    "sysUserId": null
  },
  "msg": "success",
  "code": "200"
}
```

---

### 2. 數據上傳

**POST** `/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3`

**Headers**:
```
accessToken: autoprt7d4e2f8a9b1c3d5e6f7a8b9c0d1e2f3a
Content-Type: application/json
```

**請求 Body**:
```json
{
  "data": [
    {
      "traceCode": "TRACE-001",
      "lotNo": "LOT-2024-001",
      "procName": "鑽孔",
      "devName": "鑽孔機-01",
      "userName": "操作員A",
      "partNumber": "PART-12345",
      "moldNo": "MOLD-001",
      "woNum": "WO-2024-001"
    }
  ],
  "isVerifyLot": true
}
```

**響應**:
```json
{
  "success": true,
  "data": null,
  "msg": "上傳成功",
  "code": "200"
}
```

---

### 3. 追溯碼驗證

**POST** `/CimforceTraceMgrDev/api/transcode/checkcode`

**Headers**:
```
accessToken: autoprt7d4e2f8a9b1c3d5e6f7a8b9c0d1e2f3a
Content-Type: application/json
```

**請求 Body**:
```json
{
  "woNum": "WO-2024-001",
  "codes": ["CODE-001", "CODE-002"]
}
```

**響應**:
```json
{
  "success": true,
  "data": null,
  "msg": "驗證成功",
  "code": "200"
}
```

---

## 🎮 模擬設定

### 模擬模式

1. **正常響應**: 所有請求都返回成功 (Code 200)
2. **總是失敗**: 所有請求都返回失敗 (模擬業務錯誤)
3. **隨機故障**: 根據故障率隨機返回成功或失敗
4. **延遲響應**: 添加延遲時間模擬網絡延遲

### 設定參數

- **延遲時間**: 0-5000 毫秒 (預設: 0ms)
- **故障率**: 0-100% (預設: 0%)

**使用方式**:
1. 選擇模擬模式
2. 調整延遲/故障率滑桿
3. 點擊 **✅ 應用設定** 按鈕

---

## 📊 通訊監控

### 請求日誌列表

顯示最新 100 條請求記錄:
- 時間
- 端點
- 客戶端 IP
- 狀態碼
- 處理時間

### 通訊詳情查看器

選擇日誌後可查看完整通訊資料:
- **📤 請求 Headers**: 完整 HTTP 請求頭 (JSON 格式)
- **📥 請求 Body**: 請求內容 (JSON 格式)
- **📤 響應 Body**: 響應內容 (JSON 格式)

---

## 🧪 測試範例 (Postman / curl)

### 1. 設備登入取得 Token

```bash
curl -X POST http://localhost:5200/CimforceTraceMgrDev/api/prtmac/prtmacuserlogin \
  -H "Content-Type: application/json" \
  -d '{"prtMacNo":"TEST-MACHINE-001"}'
```

### 2. 使用 Token 上傳數據

```bash
curl -X POST http://localhost:5200/CimforceTraceMgrDev/api/v1/MesTrace/TraceData/AddData3 \
  -H "Content-Type: application/json" \
  -H "accessToken: autoprt7d4e2f8a9b1c3d5e6f7a8b9c0d1e2f3a" \
  -d '{
    "data": [
      {
        "traceCode": "TRACE-001",
        "lotNo": "LOT-2024-001",
        "procName": "鑽孔",
        "devName": "鑽孔機-01",
        "userName": "操作員A",
        "partNumber": "PART-12345",
        "moldNo": "MOLD-001",
        "woNum": "WO-2024-001"
      }
    ],
    "isVerifyLot": true
  }'
```

### 3. 驗證追溯碼

```bash
curl -X POST http://localhost:5200/CimforceTraceMgrDev/api/transcode/checkcode \
  -H "Content-Type: application/json" \
  -H "accessToken: autoprt7d4e2f8a9b1c3d5e6f7a8b9c0d1e2f3a" \
  -d '{
    "woNum": "WO-2024-001",
    "codes": ["CODE-001", "CODE-002"]
  }'
```

---

## 🔧 整合測試 (與 MesMiddleware.Service)

### 1. 修改 MesMiddleware.Service 配置

編輯 `src/MesMiddleware.Service/appsettings.json`:

```json
{
  "WebApi": {
    "BaseUrl": "http://localhost:5200",
    "Username": "not_used_in_simulator",
    "Password": "not_used_in_simulator",
    "MachineNumber": "TEST-MACHINE-001",
    "MachineIp": "192.168.1.100"
  }
}
```

### 2. 啟動模擬器

```bash
cd src/MesMiddleware.Simulator
dotnet run
```

點擊 **🚀 啟動服務器** 和 **🔄 生成新 Token**

### 3. 啟動 MesMiddleware.Service

```bash
cd src/MesMiddleware.Service
dotnet run
```

### 4. 測試數據上傳

使用 Postman 或 curl 向 MesMiddleware.Service 發送檢測數據:

```bash
curl -X POST http://localhost:5100/api/inspection/submit \
  -H "Content-Type: application/json" \
  -d '{
    "traceCode": "TRACE-001",
    "lotNo": "LOT-2024-001",
    "procName": "鑽孔",
    "devName": "鑽孔機-01",
    "userName": "操作員A"
  }'
```

### 5. 監控通訊

在模擬器視窗查看:
- 統計卡片更新 (成功數 +1)
- 請求日誌顯示新記錄
- 通訊詳情顯示完整 JSON

---

## 🗃️ 資料庫

模擬器使用 SQLite 資料庫: `simulator.db`

### 資料表

1. **RequestLogs**: 完整通訊日誌
2. **DeviceLogins**: 設備登入記錄 (含 Token)
3. **TraceDataRecords**: 追溯數據上傳記錄

### 清空資料

點擊 **🗑️ 清空** 按鈕可清空所有日誌記錄 (會刪除測試 Token,需重新生成)

---

## 📖 Token 機制說明

### Token 生命週期

1. **生成**: 設備登入時自動生成 (格式: `autoprt` + 32位GUID)
2. **綁定**: Token 與機台編號 (PrtMacNo) 和 IP 地址綁定
3. **驗證**: 每次 API 調用從 Header `accessToken` 讀取並驗證
4. **更新**: 每次成功調用更新 `LastUsedTime`
5. **失效**: 目前無自動過期,需手動清空資料庫

### Token 安全機制

- ✅ IP 綁定: 同一機台從不同 IP 登入會被拒絕 (Code 300)
- ✅ 有效性檢查: `IsActive = true` 才能使用
- ✅ 資料庫持久化: 重啟模擬器後 Token 仍然有效

---

## 🐛 常見問題

### Q: Token 無效錯誤 (Code 401)?

A: 確認:
1. Token 已通過「生成新 Token」按鈕生成
2. Header 名稱正確: `accessToken` (不是 `Authorization`)
3. Token 完整複製,沒有多餘空格

### Q: IP 不匹配錯誤 (Code 300)?

A: 測試設備固定 IP 為 `192.168.1.100`,如果從其他 IP 請求會被拒絕。需重新登入或清空資料庫。

### Q: 如何重置所有資料?

A: 點擊 **🗑️ 清空** 按鈕,或手動刪除 `simulator.db` 檔案。

---

## 📞 技術支援

如有問題請查看:
- Swagger UI: `http://localhost:5200/swagger`
- 日誌檔案: `logs/simulator-*.log`
- 通訊詳情: 選擇請求日誌查看完整 JSON
