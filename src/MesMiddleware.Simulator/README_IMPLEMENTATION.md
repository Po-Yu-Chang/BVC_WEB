# MES Cloud Simulator 實作指南

## 專案結構已創建

```
MesMiddleware.Simulator/
├── Models/
│   ├── RequestLog.cs ✅           # HTTP 請求日誌 (完整通訊資料)
│   ├── DeviceLogin.cs ✅          # 設備登錄記錄
│   ├── TraceDataRecord.cs ✅      # 追溯數據記錄
│   └── MesApiModels.cs ✅         # MES API 請求/響應模型
├── Data/
│   └── SimulatorDbContext.cs ✅   # EF Core SQLite DbContext
├── Services/
│   ├── TokenService.cs ✅         # Token 生成與驗證
│   └── SimulationConfigService.cs ✅  # 模擬行為配置
├── Controllers/                    # 待實作
├── ViewModels/                     # 待實作
└── Views/                          # 待實作
```

## 下一步: 創建 API Controllers

需要創建 3 個 Controller:

### 1. PrtMacController.cs
```csharp
[ApiController]
[Route("CimforceTraceMgrDev/api/prtmac")]
public class PrtMacController : ControllerBase
{
    // POST /api/prtmac/prtmacuserlogin
    [HttpPost("prtmacuserlogin")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
}
```

### 2. MesTraceController.cs
```csharp
[ApiController]
[Route("CimforceTraceMgrDev/api/v1/MesTrace/TraceData")]
public class MesTraceController : ControllerBase
{
    // POST /api/v1/MesTrace/TraceData/AddData3
    [HttpPost("AddData3")]
    public async Task<IActionResult> AddData3([FromBody] TraceDataRequest request)
}
```

### 3. TransCodeController.cs
```csharp
[ApiController]
[Route("CimforceTraceMgrDev/api/transcode")]
public class TransCodeController : ControllerBase
{
    // POST /api/transcode/checkcode
    [HttpPost("checkcode")]
    public async Task<IActionResult> CheckCode([FromBody] CheckCodeRequest request)
}
```

## WPF UI 設計

主界面需要顯示:

1. **服務器狀態區** (頂部)
   - 運行狀態燈號 (綠/紅)
   - 監聽埠: http://localhost:5200
   - 啟動/停止按鈕

2. **統計資訊區** (中部卡片)
   - 總請求數
   - 成功數 / 失敗數
   - 已登錄設備數
   - 數據上傳筆數

3. **請求日誌列表** (主要區域 - DataGrid)
   列: 時間 | 端點 | 客戶端IP | 狀態 | 耗時(ms) | 詳情按鈕

4. **通訊詳情查看器** (右側面板或彈窗)
   - Tab 1: 請求 Headers (JSON 格式化)
   - Tab 2: 請求 Body (JSON 格式化,語法高亮)
   - Tab 3: 響應 Body (JSON 格式化,語法高亮)

5. **模擬設定區** (底部)
   - 模擬模式: [正常/總是失敗/隨機故障/延遲]
   - 延遲時間: [0-5000ms] 滑桿
   - 故障率: [0-100%] 滑桿

## 待完成任務

由於代碼量大,建議分批實作:

**第一批 (核心功能)**:
- [ ] 創建 3 個 API Controllers
- [ ] 創建 RequestLoggingMiddleware
- [ ] 創建 Program.cs (Web API 啟動)
- [ ] 初始化數據庫遷移

**第二批 (WPF UI)**:
- [ ] 創建 MainViewModel
- [ ] 創建 MainWindow.xaml UI
- [ ] 實作請求日誌即時顯示

**第三批 (高級功能)**:
- [ ] JSON 查看器 (語法高亮)
- [ ] 模擬設定 UI
- [ ] Swagger UI 集成

---

**您想讓我繼續完成哪一批? 或者您想要我創建完整的代碼文件?**
