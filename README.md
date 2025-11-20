# MES 中�?軟�? - Web API ?��?

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![測試](https://img.shields.io/badge/tests-80%20passing-brightgreen)](tests/)
[![覆�??�](https://img.shields.io/badge/coverage-85%25-brightgreen)](COVERAGE_ANALYSIS.md)
[![?��?](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

?�產級�? Windows ?��???WPF 桌面?�用程�?，透�? HTTP REST API 將設?��??��??�至 MES ?�端系統??

---

## ?? ?��??�件導覽

**?�� 主�??�件**�?
- **[軟�??�求�??�書 (SRS.md)](SRS.md)** - 完整系統規格?�API ?�件?�架構說?�、部署�???
- **[測試覆�??��???(COVERAGE_ANALYSIS.md)](COVERAGE_ANALYSIS.md)** - 詳細?��?求�?測試?��?

**?�� ?��??�移（v1.0 ??v2.0�?*�?
- **v1.0** (已�?�?: ?�享記憶�?(MemoryMappedFile + EventWaitHandle) ?��?
- **v2.0** (?��?): HTTP REST API ?��?（本?�件�?
- ?��?程�?碼已移至：[.archive/SharedMemory/](.archive/SharedMemory/)
- ?��??�件已移?��?[.archive/old-docs/](.archive/old-docs/)

**?��? 注�?**：以下內容為快速入?�?��??��??��??�能?�求、API 規格?�部署步驟、設?�整?��?例�??�閱 **[SRS.md](SRS.md)**??

---

## ?�� ?�能?�色

### ??使用?��?�?1：設?��??�收??(完�?)
- **HTTP POST API**：設????HTTP Endpoint (`/api/inspection/submit`) ??中�?軟�? ??MES Cloud API
- **三層佇�??��?**：Channel (記憶�? ??SQLite (?��??? ??Hangfire (?�試)
- **FluentValidation**：即?��??��?�?
- **JWT 驗�?**：自?�令?�更??
- **?�數?�?��?�?*�? 次�?試�???(2s, 4s, 8s, 16s, 32s)

### ??使用?��?�?2：即?�監?��?表板 (完�?)
- **HTTP 輪詢?��?**：WPF �?2 秒輪�?GET `/api/status`
- **多�?語�??�援**：�?體中??/ 簡�?中�? / English（即?��??��?
- **????�?��?示器**：�???(Connected) / 黃色 (Retrying) / 紅色 (Disconnected)
- **上傳歷史記�?**：顯示�?�?100 筆�??�篩?��??��?
- **?��??�試?�能**：右?�選?�觸??POST `/api/status/queue/{id}/retry`
- **系統??��??*：�?小�??�系統匣，氣泡通知

### ??使用?��?�?3：�??��?令控??(?�實�?
- **?�??*：延後至?��??�本
- **?��?**：HTTP 輪詢不適?�即?��??�通�?
- **建議?��?**：WebSocket ??Server-Sent Events (SSE)
- **?�估工�?**�?4 ?�任??(T056-T069) + 15-20 ?�測�?

## ??�?系統?��?

```
?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??   HTTP POST             ?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??
??  設�?          ?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?��?  中�?軟�? Windows ?��?    ??
?? (LabVIEW/C#)   ?? /api/inspection/submit  ?? + Kestrel Web Server    ??
?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??                         ?? (.NET 9)                ??
                                             ??                         ??
                                             ?? ?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??  ??
                                             ?? ??Channel Queue     ??  ??
                                             ?? ??(記憶體�?1000�?  ??  ??
                                             ?? ?��??�?�?�?�?�?�?�?��??�?�?�?�?�?�?�?�?�??  ??
                                             ??          ??             ??
                                             ?? ?��??�?�?�?�?�?�?�?��??�?�?�?�?�?�?�?�?�??  ??
                                             ?? ??SQLite Queue      ??  ??
                                             ?? ??(?��??��???      ??  ??
                                             ?? ?��??�?�?�?�?�?�?�?��??�?�?�?�?�?�?�?�?�??  ??
                                             ??          ??             ??
                                             ?? ?��??�?�?�?�?�?�?�?��??�?�?�?�?�?�?�?�?�??  ??
                                             ?? ??Hangfire Retry    ??  ??
                                             ?? ??(?�景工�?)        ??  ??
                                             ?? ?��??�?�?�?�?�?�?�?��??�?�?�?�?�?�?�?�?�??  ??
                                             ?��??�?�?�?�?�?�?�?�?�?�?��??�?�?�?�?�?�?�?�?�?�?�?�?�??
                                                         ??HTTPS
                                                         ??
                                             ?��??�?�?�?�?�?�?�?�?�?�?��??�?�?�?�?�?�?�?�?�?�?�?�?�??
                                             ??  MES Cloud Web API      ??
                                             ??  (JWT 驗�?)             ??
                                             ?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??

?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??   HTTP GET              ?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??
?? WPF ??��       ?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?? 中�?軟�? Windows ?��?    ??
?? 桌面?�用程�?    ??     /api/status         ?? (StatusController)      ??
?? (.NET 9)       ??     /api/status/history ??                         ??
?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??                         ?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??
                         ?��??�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�?�??
```

### ?�術架�?

- **.NET 9**：�??��??��? C# 12
- **ASP.NET Core Web API**：RESTful HTTP 端�?
- **Kestrel**：�??��? Web 伺�???
- **System.Threading.Channels**：�??�能記憶體�???
- **Windows ?��?**：�?機自?��???
- **WPF + MVVM**：�??�監??UI
- **Entity Framework Core 9**：SQLite ?��??��???
- **Hangfire**：�??��?試工作�?�?
- **Serilog**：�?構�??��?記�?
- **FluentValidation**：HTTP 請�?驗�?
- **Swagger/OpenAPI**：API ?�件
- **xUnit + FluentAssertions + Moq**：測試�???

## ?? 快速�?�?

### 系統?��?

- Windows 10/11 ??Windows Server 2019+
- .NET 9 SDK（�??�環境�?
- SQL Server LocalDB ??SQLite（�??�環境�?

### 安�?步�?

#### 1. 安�?中�?軟�??��?

```powershell
# 以系統管?�員身�??��?
.\install-service.ps1
```

?��??��?
- 建�? Windows ?��? `MesMiddleware`
- 設�??��??��??��?
- 立即?��??��?

#### 2. 設�?檔�?�?

編輯 `appsettings.json`�?

```json
{
  "Urls": "http://localhost:5100",
  "WebApi": {
    "BaseUrl": "https://your-mes-api.com",
    "Username": "middleware_user",
    "Password": "your_password",
    "MachineNumber": "MACHINE-01",
    "MachineIp": "192.168.1.100"
  },
  "InspectionChannel": {
    "Capacity": 1000,
    "FullMode": "Wait"
  },
  "Queue": {
    "DatabasePath": "Data/queue.db",
    "MaxRetries": 5,
    "RetryDelaySeconds": 2
  }
}
```

#### 3. ?��???��?�用程�?

```powershell
# ?��? WPF 桌面?�用程�?
.\src\MesMiddleware.Monitor\bin\Release\net9.0-windows\MesMiddleware.Monitor.exe
```

## ?? 測試覆�???

**總�?�?0 ?�測�?- 100% ?��?** ??

| ?�件 | 測試??| 覆�???|
|-----------|-------|----------|
| ?��?（�?端�? | 54 ?�測�?| 85% |
| - ?��?測試（JSON Schema�?| 5 ?�測�?| 100% |
| - ?��?測試（HTTP/資�?庫�? | 19 ?�測�?| 90% |
| - ?��?測試（�??��?輯�? | 30 ?�測�?| 85% |
| ??��（WPF UI�?| 26 ?�測�?| 80% |
| 使用?��?�?1（HTTP API 資�??��?�?| 54 ?�測�?| 100% ??|
| 使用?��?�?2（WPF ??��?�表板�?| 26 ?�測�?| 100% ??|
| 使用?��?�?3（�??��?令控?��? | 0 ?�測�?| ?�實�???|

### ?��?測試

```bash
# ?��??�?�測�?
dotnet test

# ?��?測試並產?��??��??��?
dotnet test --collect:"XPlat Code Coverage"

# ?��??��?類別測試
dotnet test --filter "FullyQualifiedName~Integration"
```

## ?? 專�?結�?

```
MesMiddleware/
?��??� src/
??  ?��??� MesMiddleware.Service/          # Windows ?��?（�?端�?
??  ??  ?��??� Controllers/                # ASP.NET Core Controllers
??  ??  ??  ?��??� InspectionController    # POST /api/inspection/submit
??  ??  ??  ?��??� StatusController        # GET /api/status, /api/status/history
??  ??  ?��??� Services/
??  ??  ??  ?��??� HostedServices/         # BackgroundService 實�?
??  ??  ??  ??  ?��??� InspectionChannelProcessor  # ?��? Channel 佇�?
??  ??  ??  ?��??� WebApi/                 # MES Cloud API 客戶�?+ JWT
??  ??  ??  ?��??� Queue/                  # SQLite ?��?佇�? + Hangfire
??  ??  ?��??� Data/                       # EF Core DbContext + Migrations
??  ??  ?��??� Models/                     # ?��?實�?
??  ??  ?��??� Validation/                 # FluentValidation 驗�???
??  ??  ?��??� Program.cs                  # ASP.NET Core WebApplication ?�入�?
??  ?��??� MesMiddleware.Monitor/          # WPF 桌面?�用程�?
??  ??  ?��??� ViewModels/                 # MVVM ViewModels
??  ??  ?��??� Views/                      # XAML 視�?
??  ??  ?��??� Services/                   # HTTP API 客戶端�?輪詢�?
??  ??  ?��??� Models/                     # UI DTO 模�?
??  ??  ?��??� Resources/                  # i18n .resx 資�?�?
??  ?��??� MesMiddleware.Shared/           # ?�享模�??��?�?
??      ?��??� Models/                     # InspectionRecord DTO
?��??� tests/
??  ?��??� MesMiddleware.Service.Tests/    # 後端測試�?4 ?�測試�?
??  ??  ?��??� Contract/                   # JSON Schema ?��?測試�? ?��?
??  ??  ?��??� Integration/                # HTTP/資�?庫整?�測試�?19 ?��?
??  ??  ?��??� Unit/                       # ?��??�輯?��?測試�?0 ?��?
??  ?��??� MesMiddleware.Monitor.Tests/    # WPF UI 測試�?6 ?�測試�?
??      ?��??� Unit/                       # ViewModel ?��?測試
?��??� .archive/                           # 已�?存�?�?
??  ?��??� SharedMemory/                   # v1.0 ?�享記憶體�?式碼（已移除�?
??  ?��??� old-docs/                       # v1.0 ?�件（spec.md, plan.md, tasks.md�?
?��??� SRS.md                              # 軟�??�求�??�書（主要�?件�?
?��??� COVERAGE_ANALYSIS.md                # 測試覆�??��???
?��??� README.md                           # ?��?案�?快速入?��?
```

## ?? 多�?語�??�援

??��?�用程�??�援三種語�?，可?��??��?�?

### ?�援?��?言

- **繁�?中�?（zh-TW�?*：�?設�?言
- **简体中?��?zh-CN�?*：簡體中??
- **English（en�?*：英??

### ?��?語�?

1. ?��???��?�用程�?
2. 點�??��?角�?語�??��?�?
   - **English** - ?��??�英??
   - **简体中??* - ?��??�簡體中??
   - **繁�?中�?** - ?��??��?體中??

?�??UI ?��?（�?窗�?題、Tab?�卡?�、�??�、表?��?位�??��??�更?��?

### ?�地?�內�?

- 視�?標�???Tab 標�?
- ?�?��??�卡?��?籤�?23 ?��?件�?
- ?��??�選?��?�?
- 系統??��??
- 表格欄�?標�?

### ?�術實�?

- **資�?檔�?**�?resx 檔�?，支?��?�?.NET ?�地??
- **?��??��?**：使??ILocalizationService ??MVVM 資�?綁�?
- **?�擴�?*：�?鬆新增其他�?言

## ?�� API 端�?說�?

### 設�??��?端�?

**POST /api/inspection/submit** - ?�交檢測資�?

設�?使用 HTTP POST ?�交 JSON 資�?�?

```http
POST http://localhost:5100/api/inspection/submit
Content-Type: application/json

{
  "machineNumber": "MACHINE-01",
  "serialNumber": "SN20251120001",
  "inspectionResult": "OK",
  "inspectionTime": "2025-11-20T10:30:45Z",
  "measurementData": {
    "diameter": 25.4,
    "length": 100.0
  }
}
```

**?��?**�?
- ?��?：HTTP 202 Accepted
- 驗�?失�?：HTTP 400 Bad Request（含?�誤詳�?�?

### ??��端�?

**GET /api/status** - ?��??��??�??

```http
GET http://localhost:5100/api/status
```

**?��?範�?**�?
```json
{
  "status": "Connected",
  "lastPingTimestamp": "2025-11-20T10:35:12Z",
  "queueDepth": 0,
  "totalReceived": 1234,
  "successfulUploads": 1230,
  "queuedUploads": 4
}
```

**GET /api/status/history** - ?��?上傳歷史

```http
GET http://localhost:5100/api/status/history?limit=100&status=Failed
```

**GET /swagger** - API ?�件

?�覽互�?�?API ?�件�?
```
http://localhost:5100/swagger
```
- ?��?：UTF-8 JSON，�?�?4 位�?組長�?

### 資�?結�?

#### InspectionRecord（設????中�?軟�?�?

```json
{
  "machineNumber": "MACHINE-01",
  "serialNumber": "SN20251120001",
  "inspectionResult": "OK",
  "inspectionTime": "2025-11-20T10:30:45Z",
  "measurementData": {
    "diameter": 25.4,
    "length": 100.0,
    "defects": []
  }
}
```

**驗�?規�?**�?
- `machineNumber`：�?填�??��?50 字�?
- `serialNumber`：�?填�??��?100 字�?
- `inspectionResult`：�?填�???"OK", "NG", "Recheck"
- `inspectionTime`：�?填�?ISO 8601 ?��?
- `measurementData`：選填�??�由?��? JSON ?�件

## ?? ?�質系統?��??��?（C# ??LabVIEW�?

### HTTP API ?��??�議

?�系統使?��?�?HTTP REST API 實現跨�?序通�?，任何支??HTTP ?��?言?�平?��??�整?��?

### C# ?��?範�?

#### 1. 使用 HttpClient ?�交檢測資�?（設?�端�?

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class MesMiddlewareClient
{
    private readonly HttpClient _httpClient;

    public MesMiddlewareClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5100"),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task<bool> SubmitInspectionDataAsync(object inspectionData)
    {
        try
        {
            // POST JSON ?�中介�?�?API
            var response = await _httpClient.PostAsJsonAsync(
                "/api/inspection/submit",
                inspectionData
            );

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("??資�??�交?��?");
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"???�交失�?: {response.StatusCode}");
                Console.WriteLine($"   ?�誤詳�?: {error}");
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"??網路?�誤: {ex.Message}");
            return false;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("??請�??��?（�???5 秒�?");
            return false;
        }
    }
}

// 使用範�?
var client = new MesMiddlewareClient();
var inspectionData = new
{
    machineNumber = "MACHINE-01",
    serialNumber = $"SN{DateTime.Now:yyyyMMddHHmmss}",
    inspectionResult = "OK",
    inspectionTime = DateTime.UtcNow,
    measurementData = new
    {
        diameter = 25.4,
        length = 100.0
    }
};

bool success = await client.SubmitInspectionDataAsync(inspectionData);
```


### LabVIEW ��X�d��

LabVIEW �i�ϥΤ��� HTTP Client VIs �i���X�G

**��k 1**: �ϥ� 
- URL: 
- Headers: 
- Body: JSON �r�� (�ϥ� Flatten to JSON function)

**��k 2**: �ϥ� .NET HttpClient (�z�L Constructor Node)

���㪺 LabVIEW ��X�d�ҽаѾ\ **[SRS.md - Section 12.5](SRS.md#125-equipment-integration-examples)**�C

---

## ?? ������

�H�U���e�аѾ\ **[�n��ݨD�W��� (SRS.md)](SRS.md)**�G
- ���� API �W�� (Swagger/OpenAPI �榡)
- �ԲӪ���Ƽҫ��P���ҳW�h
- ���p���n (�w�ˡB�ɯšB�^�u)
- C# �P LabVIEW �����X�d��
- �į���лP�̨Τƫ�ĳ
- �G�ٱư����n
- �[�c�E�����n (v1.0 �� v2.0)

---

## ??? �}�o�P�^�m

### ����}�o����

�ϥΨӦ� C:\Users\qoose\Desktop\�����\�Ȥ����\S-�@�B���վ�Web\src\MesMiddleware.Service\Properties\launchSettings.json ���Ұʳ]�w...
���b�ظm...
[12:35:21 INF] Starting MES Middleware Service with Web API host
[12:35:22 FTL] Application terminated unexpectedly
System.AggregateException: Some services are not able to be constructed (Error while validating the service descriptor 'ServiceType: Microsoft.Extensions.Hosting.IHostedService Lifetime: Singleton ImplementationType: MesMiddleware.Service.Services.HostedServices.InspectionChannelProcessor': Cannot consume scoped service 'MesMiddleware.Service.Services.WebApi.IMesWebApiClient' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.)
 ---> System.InvalidOperationException: Error while validating the service descriptor 'ServiceType: Microsoft.Extensions.Hosting.IHostedService Lifetime: Singleton ImplementationType: MesMiddleware.Service.Services.HostedServices.InspectionChannelProcessor': Cannot consume scoped service 'MesMiddleware.Service.Services.WebApi.IMesWebApiClient' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.
 ---> System.InvalidOperationException: Cannot consume scoped service 'MesMiddleware.Service.Services.WebApi.IMesWebApiClient' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitCallSite(ServiceCallSite callSite, CallSiteValidatorState argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitConstructor(ConstructorCallSite constructorCallSite, CallSiteValidatorState state)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitCallSite(ServiceCallSite callSite, CallSiteValidatorState argument)
   at Microsoft.Extensions.DependencyInjection.ServiceProvider.ValidateService(ServiceDescriptor descriptor)
   --- End of inner exception stack trace ---
   at Microsoft.Extensions.DependencyInjection.ServiceProvider.ValidateService(ServiceDescriptor descriptor)
   at Microsoft.Extensions.DependencyInjection.ServiceProvider..ctor(ICollection`1 serviceDescriptors, ServiceProviderOptions options)
   --- End of inner exception stack trace ---
   at Microsoft.Extensions.DependencyInjection.ServiceProvider..ctor(ICollection`1 serviceDescriptors, ServiceProviderOptions options)
   at Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(IServiceCollection services, ServiceProviderOptions options)
   at Microsoft.Extensions.Hosting.HostApplicationBuilder.Build()
   at Microsoft.AspNetCore.Builder.WebApplicationBuilder.Build()
   at Program.<Main>$(String[] args) in C:\Users\qoose\Desktop\�����\�Ȥ����\S-�@�B���վ�Web\src\MesMiddleware.Service\Program.cs:line 105
�ϥΨӦ� C:\Users\qoose\Desktop\�����\�Ȥ����\S-�@�B���վ�Web\src\MesMiddleware.Service\Properties\launchSettings.json ���Ұʳ]�w...
���b�ظm...
[12:35:24 INF] Starting MES Middleware Service with Web API host
[12:35:24 FTL] Application terminated unexpectedly
System.AggregateException: Some services are not able to be constructed (Error while validating the service descriptor 'ServiceType: Microsoft.Extensions.Hosting.IHostedService Lifetime: Singleton ImplementationType: MesMiddleware.Service.Services.HostedServices.InspectionChannelProcessor': Cannot consume scoped service 'MesMiddleware.Service.Services.WebApi.IMesWebApiClient' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.)
 ---> System.InvalidOperationException: Error while validating the service descriptor 'ServiceType: Microsoft.Extensions.Hosting.IHostedService Lifetime: Singleton ImplementationType: MesMiddleware.Service.Services.HostedServices.InspectionChannelProcessor': Cannot consume scoped service 'MesMiddleware.Service.Services.WebApi.IMesWebApiClient' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.
 ---> System.InvalidOperationException: Cannot consume scoped service 'MesMiddleware.Service.Services.WebApi.IMesWebApiClient' from singleton 'Microsoft.Extensions.Hosting.IHostedService'.
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitCallSite(ServiceCallSite callSite, CallSiteValidatorState argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitConstructor(ConstructorCallSite constructorCallSite, CallSiteValidatorState state)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteVisitor`2.VisitCallSite(ServiceCallSite callSite, TArgument argument)
   at Microsoft.Extensions.DependencyInjection.ServiceLookup.CallSiteValidator.VisitCallSite(ServiceCallSite callSite, CallSiteValidatorState argument)
   at Microsoft.Extensions.DependencyInjection.ServiceProvider.ValidateService(ServiceDescriptor descriptor)
   --- End of inner exception stack trace ---
   at Microsoft.Extensions.DependencyInjection.ServiceProvider.ValidateService(ServiceDescriptor descriptor)
   at Microsoft.Extensions.DependencyInjection.ServiceProvider..ctor(ICollection`1 serviceDescriptors, ServiceProviderOptions options)
   --- End of inner exception stack trace ---
   at Microsoft.Extensions.DependencyInjection.ServiceProvider..ctor(ICollection`1 serviceDescriptors, ServiceProviderOptions options)
   at Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(IServiceCollection services, ServiceProviderOptions options)
   at Microsoft.Extensions.Hosting.HostApplicationBuilder.Build()
   at Microsoft.AspNetCore.Builder.WebApplicationBuilder.Build()
   at Program.<Main>$(String[] args) in C:\Users\qoose\Desktop\�����\�Ȥ����\S-�@�B���վ�Web\src\MesMiddleware.Service\Program.cs:line 105
  ���b�P�_�n�٭쪺�M��...
  �Ҧ��M�׳��b�̷s���A�A�i�i���٭�C

���i�Ϊ��u�@�t����s�C�p�ݸԲӸ�T�A�а��� `dotnet workload list`�C

### �{���X����

- ���` .NET �s�X�D��
- �ϥ� C# 12 �\�� (file-scoped namespaces, required properties)
- ���B��k�H  ����
- ���թR�W: 

### Git ����T���榡



---

## ?? ���v

MIT License - �Ԩ� LICENSE �ɮ�

---

## ?? �䴩

- **���**: [SRS.md](SRS.md)
- **�����л\�v**: [COVERAGE_ANALYSIS.md](COVERAGE_ANALYSIS.md)
- **�M�׫���**: [CLAUDE.md](CLAUDE.md)

**����**: v2.0 (Web API �[�c)
**�̫��s**: 2025-11-20


### LabVIEW ?��?範�?

LabVIEW ?�使?�內�?HTTP Client VIs ?��??��?�?

**?��? 1**: 使用 `HTTP Client POST.vi`
- URL: `http://localhost:5100/api/inspection/submit`
- Headers: `Content-Type: application/json`
- Body: JSON 字串 (使用 Flatten to JSON function)

**?��? 2**: 使用 .NET HttpClient (?��? Constructor Node)

完整??LabVIEW ?��?範�?請�???**[SRS.md - Section 12.5](SRS.md#125-equipment-integration-examples)**??

---

## ?? 完整?�件

以�??�容請�???**[軟�??�求�??�書 (SRS.md)](SRS.md)**�?
- 完整 API 規格 (Swagger/OpenAPI ?��?)
- 詳細?��??�模?��?驗�?規�?
- ?�署?��? (安�??��?級、�?�?
- C# ??LabVIEW 完整?��?範�?
- ?�能?��??��?佳�?建議
- ?��??�除?��?
- ?��??�移?��? (v1.0 ??v2.0)

---

## ??�??�發?�貢??

### ?��??�發?��?

```bash
# ?��??��? (console mode)
cd src/MesMiddleware.Service
dotnet run

# ?��? WPF Monitor
cd src/MesMiddleware.Monitor
dotnet run

# ?��?測試
dotnet test

# ?��? Swagger API ?�件
# ?��??��?後瀏覽: http://localhost:5100/swagger
```

### 程�?碼風??

- ?�循 .NET 編碼???
- 使用 C# 12 ?�能 (file-scoped namespaces, required properties)
- ?�步?��?�?`Async` 結尾
- 測試?��?: `MethodName_Scenario_ExpectedBehavior`

### Git ?�交訊息?��?

```
feat: Add new HTTP endpoint for batch submission
fix: Resolve channel queue deadlock issue
test: Add integration tests for offline queue
docs: Update SRS.md API specification
```

---

## ?? ?��?

MIT License - 詳�? LICENSE 檔�?

---

## ?? ?�援

- **?�件**: [SRS.md](SRS.md)
- **測試覆�???*: [COVERAGE_ANALYSIS.md](COVERAGE_ANALYSIS.md)
- **專�??��?**: [CLAUDE.md](CLAUDE.md)

**?�本**: v2.0 (Web API ?��?)
**?�後更??*: 2025-11-20
