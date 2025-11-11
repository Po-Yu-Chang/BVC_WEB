# Tasks: Shared Memory Middleware Service

**Input**: Design documents from `/specs/002-shared-memory-middleware/`
**Prerequisites**: plan.md (required), spec.md (required), research.md (optional)

**Tests**: TDD Red-Green-Refactor cycle is MANDATORY per Constitution Principle III. All tests written FIRST before implementation.

**Organization**: Tasks grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Service**: `src/MesMiddleware.Service/`
- **Monitor (WPF)**: `src/MesMiddleware.Monitor/`
- **Shared**: `src/MesMiddleware.Shared/`
- **Tests**: `tests/MesMiddleware.Service.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create solution structure with MesMiddleware.Service, MesMiddleware.Monitor, MesMiddleware.Shared projects per plan.md
- [X] T002 Initialize .NET 9 projects: dotnet new worker for Service, dotnet new wpf for Monitor, dotnet new classlib for Shared
- [X] T003 [P] Install Service dependencies: Microsoft.Extensions.Hosting, Serilog, EF Core, Hangfire, HttpClient.Resilience, FluentValidation per research.md
- [X] T004 [P] Install Monitor dependencies: WPF .NET 9, H.NotifyIcon.Wpf (system tray), Microsoft.Extensions.DependencyInjection per research.md
- [X] T005 [P] Install Shared dependencies: System.Text.Json per plan.md
- [X] T006 [P] Install test dependencies: xUnit, FluentAssertions, WireMock.Net per research.md
- [X] T007 Configure Serilog in src/MesMiddleware.Service/Program.cs with file + console sinks, daily rolling, 7-day retention per research.md R6
- [X] T008 Create appsettings.json in src/MesMiddleware.Service/ with WebAPI URLs, shared memory config (segment names "MES_INSPECTION_DATA"/"MES_EQUIPMENT_CMD", 10MB size), retry policies per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**  CRITICAL**: No user story work can begin until this phase is complete

- [X] T009 Create SharedMemoryMessage.cs in src/MesMiddleware.Shared/Models/ with InspectionRecord, ParamDataItem, BenchmarkItem, OtherDataItem per spec.md data contract
- [X] T010 [P] Create QueuedUpload.cs entity in src/MesMiddleware.Service/Models/ with Id, InspectionDataJson, QueuedAt, RetryCount, NextRetryAt, LastError per plan.md offline queue
- [X] T011 [P] Create MiddlewareDbContext.cs in src/MesMiddleware.Service/Data/ with QueuedUploads DbSet, SQLite configuration per research.md R5
- [X] T012 Create EF Core migration for QueuedUploads table: dotnet ef migrations add InitialCreate in src/MesMiddleware.Service/
- [X] T013 [P] Create ISharedMemoryMonitor.cs interface in src/MesMiddleware.Service/Services/SharedMemory/ with StartAsync, StopAsync, OnDataReceived event
- [X] T014 [P] Create IMesWebApiClient.cs interface in src/MesMiddleware.Service/Services/WebApi/ with AuthenticateAsync, UploadInspectionDataAsync methods
- [X] T015 [P] Create IUploadQueueService.cs interface in src/MesMiddleware.Service/Services/Queue/ with EnqueueAsync, RetryUploadAsync methods
- [X] T016 Configure Windows Service hosting in src/MesMiddleware.Service/Program.cs with UseWindowsService(), AddHostedService<MiddlewareHostedService>() per research.md R2

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Equipment Data Collection via Shared Memory (Priority: P1) <¯ MVP

**Goal**: Automated data flow from equipment (via shared memory) to WebAPI with retry/queue logic

**Independent Test**: Simulate equipment writing JSON to MemoryMappedFile, verify middleware detects within 1s, deserializes correctly, POSTs to WebAPI successfully. Queue and retry on WebAPI failure.

### Tests for User Story 1 (TDD Red-Green-Refactor)  

> **=4 RED PHASE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T017 [P] [US1] Contract test for shared memory JSON format in tests/MesMiddleware.Service.Tests/Contract/SharedMemoryFormatTests.cs - validates equipment JSON matches WebAPI InspectionDataRequest schema
- [X] T018 [P] [US1] Integration test for shared memory IPC in tests/MesMiddleware.Service.Tests/Integration/SharedMemoryIpcTests.cs - mock equipment writes to MemoryMappedFile, verify middleware reads within 1s
- [X] T019 [P] [US1] Integration test for WebAPI upload in tests/MesMiddleware.Service.Tests/Integration/WebApiUploadTests.cs - use WireMock to mock WebAPI, verify POST /api/inspection/upload with accessToken header
- [X] T020 [P] [US1] Integration test for offline queue in tests/MesMiddleware.Service.Tests/Integration/OfflineQueueTests.cs - mock WebAPI unavailable (HTTP 503), verify data queued to SQLite, verify retry after recovery
- [X] T021 [P] [US1] Unit test for SharedMemoryMonitor in tests/MesMiddleware.Service.Tests/Unit/SharedMemoryMonitorTests.cs - test MemoryMappedFile read, EventWaitHandle signal, mutex locking
- [X] T022 [P] [US1] Unit test for TokenService in tests/MesMiddleware.Service.Tests/Unit/TokenServiceTests.cs - test token caching, refresh on expiration (8 hours), SemaphoreSlim thread-safety
- [X] T023 [P] [US1] Unit test for UploadQueueService in tests/MesMiddleware.Service.Tests/Unit/UploadQueueServiceTests.cs - test EF Core queue operations, FIFO order, Hangfire job enqueue

**Run: dotnet test** ’ Expected: ALL TESTS FAIL (endpoints/services not implemented)

### Implementation for User Story 1 (Green + Refactor Phases)

> **=â GREEN PHASE: Implement MINIMUM code to make tests pass**

- [X] T024 [P] [US1] Implement TokenService.cs in src/MesMiddleware.Service/Services/WebApi/ - IMemoryCache for tokens, SemaphoreSlim for thread-safe refresh, 8-hour expiration per research.md R2
- [X] T025 [P] [US1] Implement MesWebApiClient.cs in src/MesMiddleware.Service/Services/WebApi/ - HttpClient with standard resilience handler (retry 2s/4s/8s/16s/32s, circuit breaker), POST /api/auth/login, POST /api/inspection/upload per research.md R4
- [X] T026 [US1] Implement SharedMemoryMonitor.cs in src/MesMiddleware.Service/Services/SharedMemory/ - MemoryMappedFile.OpenExisting("MES_INSPECTION_DATA"), EventWaitHandle.WaitOne(), Mutex for exclusive access, UTF-8 JSON deserialization, OnDataReceived event per research.md R1
- [X] T027 [US1] Implement UploadQueueService.cs in src/MesMiddleware.Service/Services/Queue/ - EF Core QueuedUploads operations, Hangfire BackgroundJob.Enqueue for retry, exponential backoff per research.md R5
- [X] T028 [US1] Create InspectionDataValidator.cs in src/MesMiddleware.Service/Validation/ - FluentValidation rules: rowNo > 0, procName/devName/userName/workClass required, traceCode OR lotNo required per spec.md FR-009
- [X] T029 [US1] Implement MiddlewareHostedService.cs in src/MesMiddleware.Service/Services/HostedServices/ - BackgroundService ExecuteAsync loop: StartAsync SharedMemoryMonitor, subscribe to OnDataReceived event, validate with InspectionDataValidator, call MesWebApiClient.UploadInspectionDataAsync, queue on failure
- [X] T030 [US1] Configure Hangfire in src/MesMiddleware.Service/Program.cs - AddHangfire with SQLite storage, UseHangfireServer with 1 worker, map /hangfire dashboard endpoint per research.md R5
- [X] T031 [US1] Register all services in src/MesMiddleware.Service/Program.cs dependency injection: AddSingleton<ISharedMemoryMonitor>, AddHttpClient<IMesWebApiClient> with resilience handler, AddScoped<IUploadQueueService>

**Run: dotnet test** ’ Expected: ALL TESTS PASS (minimum implementation complete)

> **=5 REFACTOR PHASE: Improve code quality while keeping tests green**

- [X] T032 [US1] Refactor SharedMemoryMonitor: extract ISharedMemorySegment interface, add CancellationToken support for graceful shutdown, improve error logging with Serilog context (segment name, data size, latency)
- [X] T033 [US1] Refactor MesWebApiClient: extract retry policy configuration to appsettings.json, add HttpContext logging for all requests (URL, status code, duration), implement IDisposable for HttpClient lifecycle
- [X] T034 [US1] Refactor UploadQueueService: add batch retry processing (process up to 10 queued items per job), add dead letter queue logic (move to Failed state after 5 retries), log retry attempts with structured context

**Run: dotnet test** ’ Expected: ALL TESTS STILL PASS (refactored code maintains behavior)

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. Equipment ’ Shared Memory ’ Middleware ’ WebAPI data pipeline works with offline queueing.

---

## Phase 4: User Story 2 - Real-Time Monitoring Dashboard (Priority: P2)

**Goal**: WPF desktop application for operators to monitor middleware health, view upload history, inspect shared memory activity, minimize to system tray

**Independent Test**: Launch WPF application, verify UI displays connection status (green/red/yellow), view recent uploads (timestamps, trace codes, success/failure), view shared memory activity log, minimize to tray, receive error notifications.

### Tests for User Story 2 (TDD Red-Green-Refactor)  

> **=4 RED PHASE: Write these tests FIRST**

- [X] T035 [P] [US2] Unit test for StatusViewModel in tests/MesMiddleware.Monitor.Tests/Unit/StatusViewModelTests.cs - test INotifyPropertyChanged, connection status states (Connected/Disconnected/Retrying), last ping timestamp
- [X] T036 [P] [US2] Unit test for HistoryViewModel in tests/MesMiddleware.Monitor.Tests/Unit/HistoryViewModelTests.cs - test ObservableCollection<UploadRecord>, filtering, paging (1000 max records)
- [X] T037 [P] [US2] Unit test for TrayIconService in tests/MesMiddleware.Monitor.Tests/Unit/TrayIconServiceTests.cs - test icon state changes (green/red/yellow), balloon notifications, minimize/restore behavior

**Run: dotnet test** ’ Expected: TESTS FAIL (ViewModels not implemented)

### Implementation for User Story 2 (Green + Refactor Phases)

> **=â GREEN PHASE**

- [X] T038 [P] [US2] Create UploadRecord.cs model in src/MesMiddleware.Monitor/Models/ with Timestamp, TraceCode, EquipmentName, Status (Success/Failed/Pending), ErrorMessage properties
- [X] T039 [P] [US2] Create SharedMemoryActivity.cs model in src/MesMiddleware.Monitor/Models/ with Timestamp, EventType (Read/Write), DataSize, ProcessingStatus properties
- [X] T040 [P] [US2] Create IMiddlewareApiClient.cs interface in src/MesMiddleware.Monitor/Services/ with GetConnectionStatusAsync, GetUploadHistoryAsync, GetSharedMemoryActivityAsync methods
- [X] T041 [US2] Implement MiddlewareApiClient.cs in src/MesMiddleware.Monitor/Services/ - HTTP GET to middleware service health endpoint (http://localhost:5000/health), read SQLite database directly for history (or poll log file)
- [X] T042 [US2] Implement StatusViewModel.cs in src/MesMiddleware.Monitor/ViewModels/ - MVVM pattern, INotifyPropertyChanged, poll MiddlewareApiClient every 2 seconds via DispatcherTimer, update ConnectionStatus property
- [X] T043 [US2] Implement HistoryViewModel.cs in src/MesMiddleware.Monitor/ViewModels/ - ObservableCollection<UploadRecord>, load from MiddlewareApiClient, filter/search commands
- [X] T044 [US2] Implement SharedMemoryViewModel.cs in src/MesMiddleware.Monitor/ViewModels/ - ObservableCollection<SharedMemoryActivity>, load from MiddlewareApiClient or Serilog file, auto-scroll to latest
- [X] T045 [US2] Implement TrayIconService.cs in src/MesMiddleware.Monitor/Services/ - H.NotifyIcon.Wpf TaskbarIcon, set icon based on ConnectionStatus (green.ico/red.ico/yellow.ico), ShowBalloonTip on errors
- [X] T046 [US2] Create MainWindow.xaml in src/MesMiddleware.Monitor/Views/ - TabControl with 3 tabs (Status, Upload History, Shared Memory Activity), bind to MainViewModel
- [X] T047 [US2] Create StatusView.xaml in src/MesMiddleware.Monitor/Views/ - display ConnectionStatus with colored indicator, last ping timestamp, current queue depth
- [X] T048 [US2] Create HistoryView.xaml in src/MesMiddleware.Monitor/Views/ - DataGrid bound to HistoryViewModel.UploadRecords, columns: Timestamp, TraceCode, Equipment, Status, Error
- [X] T049 [US2] Create SharedMemoryView.xaml in src/MesMiddleware.Monitor/Views/ - ListView bound to SharedMemoryViewModel.Activities, auto-scroll to bottom
- [X] T050 [US2] Create MainViewModel.cs in src/MesMiddleware.Monitor/ViewModels/ - aggregate ViewModels (Status, History, SharedMemory), handle tab switching
- [X] T051 [US2] Wire up dependency injection in src/MesMiddleware.Monitor/App.xaml.cs - ServiceCollection, register ViewModels, IMiddlewareApiClient, TrayIconService
- [X] T052 [US2] Implement minimize to tray in src/MesMiddleware.Monitor/MainWindow.xaml.cs - handle Window.StateChanged event, hide window when minimized, show on tray icon click

**Run: dotnet test** ’ Expected: TESTS PASS

> **=5 REFACTOR PHASE**

- [X] T053 [US2] Refactor ViewModels: extract base class BaseViewModel with INotifyPropertyChanged boilerplate, add async command support (RelayCommand), add loading indicators
- [X] T054 [US2] Refactor MiddlewareApiClient: add caching for history/activity (5-second cache), add retry logic for HTTP failures, add timeout (10s)
- [X] T055 [US2] Refactor UI: add WPF themes (light/dark mode toggle), improve DataGrid styling with alternating row colors, add search/filter textboxes

**Run: dotnet test** ’ Expected: TESTS STILL PASS

**Checkpoint**: User Stories 1 AND 2 now work independently. Operators can collect data (US1) and monitor system health (US2) without one affecting the other.

---

## Phase 5: User Story 3 - Bidirectional Command & Control (Priority: P3)

**Goal**: Remote equipment commands from WebAPI ’ Middleware ’ Shared Memory ’ Equipment, with acknowledgment tracking and timeout handling

**Independent Test**: Call WebAPI POST /api/equipment/command, verify middleware writes to MES_EQUIPMENT_CMD shared memory, simulate equipment reading command and acknowledging, verify middleware POSTs ack back to WebAPI. Handle timeout (30s) if no ack.

### Tests for User Story 3 (TDD Red-Green-Refactor)  

> **=4 RED PHASE**

- [ ] T056 [P] [US3] Integration test for command write in tests/MesMiddleware.Service.Tests/Integration/CommandWriteTests.cs - mock WebAPI sends command, verify middleware writes to MES_EQUIPMENT_CMD shared memory
- [ ] T057 [P] [US3] Integration test for command acknowledgment in tests/MesMiddleware.Service.Tests/Integration/CommandAckTests.cs - simulate equipment acknowledging command via shared memory response segment, verify middleware POSTs ack to WebAPI
- [ ] T058 [P] [US3] Integration test for command timeout in tests/MesMiddleware.Service.Tests/Integration/CommandTimeoutTests.cs - simulate equipment NOT acknowledging within 30s, verify middleware logs timeout and notifies WebAPI of failure
- [ ] T059 [P] [US3] Unit test for SharedMemoryWriter in tests/MesMiddleware.Service.Tests/Unit/SharedMemoryWriterTests.cs - test MemoryMappedFile write, Mutex locking, UTF-8 encoding

**Run: dotnet test** ’ Expected: TESTS FAIL

### Implementation for User Story 3 (Green + Refactor Phases)

> **=â GREEN PHASE**

- [ ] T060 [P] [US3] Create EquipmentCommand.cs model in src/MesMiddleware.Shared/Models/ with CommandId, CommandType, Parameters, IssuedAt properties
- [ ] T061 [P] [US3] Create ISharedMemoryWriter.cs interface in src/MesMiddleware.Service/Services/SharedMemory/ with WriteCommandAsync, WaitForAcknowledgmentAsync methods
- [ ] T062 [US3] Implement SharedMemoryWriter.cs in src/MesMiddleware.Service/Services/SharedMemory/ - MemoryMappedFile.CreateOrOpen("MES_EQUIPMENT_CMD"), write JSON command, signal EventWaitHandle, WaitForAcknowledgmentAsync with 30s timeout per research.md R1
- [ ] T063 [US3] Add command endpoint to MesWebApiClient.cs in src/MesMiddleware.Service/Services/WebApi/ - POST /api/equipment/command/ack with command ID and status (success/failed/timeout)
- [ ] T064 [US3] Add command handling to MiddlewareHostedService.cs in src/MesMiddleware.Service/Services/HostedServices/ - subscribe to command channel (polling WebAPI or SignalR), write command via SharedMemoryWriter, await acknowledgment, POST ack to WebAPI
- [ ] T065 [US3] Create CommandHistoryView.xaml in src/MesMiddleware.Monitor/Views/ - DataGrid with columns: Timestamp, CommandType, Status (Pending/Success/Failed/Timeout), ErrorMessage
- [ ] T066 [US3] Create CommandHistoryViewModel.cs in src/MesMiddleware.Monitor/ViewModels/ - ObservableCollection<CommandRecord>, load from middleware service command history API or database
- [ ] T067 [US3] Add CommandHistory tab to MainWindow.xaml in src/MesMiddleware.Monitor/Views/ - bind to CommandHistoryViewModel

**Run: dotnet test** ’ Expected: TESTS PASS

> **=5 REFACTOR PHASE**

- [ ] T068 [US3] Refactor SharedMemoryWriter: add retry logic for write failures (3 attempts), add validation for command JSON schema, improve logging with command details
- [ ] T069 [US3] Refactor command flow: extract ICommandOrchestrator interface, add command queue for multiple concurrent commands, add priority handling (urgent vs. normal)

**Run: dotnet test** ’ Expected: TESTS STILL PASS

**Checkpoint**: All user stories (US1, US2, US3) now fully functional independently. System supports bidirectional communication: equipment data ’ cloud (US1), monitoring (US2), cloud commands ’ equipment (US3).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T070 [P] Add health check endpoint in src/MesMiddleware.Service/Program.cs - app.MapHealthChecks("/health"), check shared memory availability, WebAPI connectivity, queue depth
- [X] T071 [P] Add graceful shutdown logic in MiddlewareHostedService.cs - override StopAsync, complete in-flight uploads before exiting, flush Serilog logs
- [X] T072 [P] Add Windows Service installer script - install-service.ps1 with sc.exe create commands, service description, auto-start configuration
- [X] T073 [P] Create appsettings.Production.json in src/MesMiddleware.Service/ with production WebAPI URL, log level = Information (not Debug)
- [X] T074 [P] Add WPF application installer - create .msi with WiX Toolset or publish self-contained .exe for easy deployment
- [X] T075 [P] Document configuration in specs/002-shared-memory-middleware/quickstart.md - appsettings.json keys, shared memory segment names, WebAPI URLs, machine number/IP
- [X] T076 Add performance metrics to Serilog - log middleware latency (shared memory read ’ WebAPI POST), queue depth, success rate every 60 seconds
- [X] T077 [P] Add error recovery scenarios - handle shared memory segment deleted at runtime (recreate), handle SQLite database corruption (backup + recreate)
- [X] T078 Create equipment simulation tool in tests/EquipmentSimulator/ - console app that writes test JSON to MES_INSPECTION_DATA shared memory for manual testing
- [X] T079 Run quickstart.md validation - follow setup guide, verify middleware service starts, verify WPF UI launches, verify simulated equipment data flows to WebAPI

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User Story 1 (P1): Can start after Foundational - No dependencies on other stories  MVP
  - User Story 2 (P2): Can start after Foundational - May integrate with US1 (read upload history) but independently testable
  - User Story 3 (P3): Can start after Foundational - May integrate with US2 (command history UI) but independently testable
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Reads upload history from US1's SQLite queue (soft dependency, can mock)
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Command history displayed in US2's WPF UI (soft dependency, can add later)

### Within Each User Story (TDD Flow)

- **=4 RED**: Write tests FIRST, verify they FAIL (missing implementation)
- **=â GREEN**: Implement MINIMUM code to make tests pass (no refactoring yet)
- **=5 REFACTOR**: Improve code quality while keeping tests green
- Commit after each successful Red-Green-Refactor cycle

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T003-T008)
- All Foundational tasks marked [P] can run in parallel within Phase 2 (T010-T015)
- Once Foundational completes, all user stories (US1, US2, US3) can start in parallel (if team capacity allows)
- All tests within a user story marked [P] can be written in parallel (e.g., T017-T023 for US1)
- Models/services marked [P] within a story can be implemented in parallel (e.g., T024-T025 for US1)

---

## Parallel Example: User Story 1 (Equipment Data Collection)

```bash
# RED PHASE: Launch all tests together (expect failures)
Task: "Contract test for shared memory JSON format in tests/.../Contract/SharedMemoryFormatTests.cs"
Task: "Integration test for shared memory IPC in tests/.../Integration/SharedMemoryIpcTests.cs"
Task: "Integration test for WebAPI upload in tests/.../Integration/WebApiUploadTests.cs"
Task: "Integration test for offline queue in tests/.../Integration/OfflineQueueTests.cs"
Task: "Unit test for SharedMemoryMonitor in tests/.../Unit/SharedMemoryMonitorTests.cs"
Task: "Unit test for TokenService in tests/.../Unit/TokenServiceTests.cs"
Task: "Unit test for UploadQueueService in tests/.../Unit/UploadQueueServiceTests.cs"

# GREEN PHASE: Implement services in parallel (different files)
Task: "Implement TokenService.cs in src/.../Services/WebApi/"
Task: "Implement MesWebApiClient.cs in src/.../Services/WebApi/"
# (SharedMemoryMonitor depends on data from equipment, sequential within shared memory module)
Task: "Implement SharedMemoryMonitor.cs in src/.../Services/SharedMemory/"
Task: "Implement UploadQueueService.cs in src/.../Services/Queue/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T008)
2. Complete Phase 2: Foundational (T009-T016) - CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T017-T034)
4. **STOP and VALIDATE**: Test US1 independently with equipment simulator
5. Deploy/demo if ready - automated equipment data collection works!

### Incremental Delivery (All Stories)

1. Complete Setup + Foundational ’ Foundation ready
2. Add User Story 1 (T017-T034) ’ Test independently with simulator ’ Deploy/Demo (MVP!)
3. Add User Story 2 (T035-T055) ’ Test independently (WPF UI works without US1 running) ’ Deploy/Demo
4. Add User Story 3 (T056-T069) ’ Test independently (commands work) ’ Deploy/Demo
5. Add Polish (T070-T079) ’ Final production-ready release

### Parallel Team Strategy

With multiple developers (after Foundational phase completes):

1. Team completes Setup + Foundational together (T001-T016)
2. Once Foundational done (checkpoint reached):
   - Developer A: User Story 1 (Equipment Data Collection) - T017-T034
   - Developer B: User Story 2 (WPF Monitoring UI) - T035-T055
   - Developer C: User Story 3 (Bidirectional Commands) - T056-T069
3. Stories complete and integrate independently
4. Team reconvenes for Polish phase (T070-T079)

---

## Notes

- **[P] tasks**: Different files, no dependencies - safe to parallelize
- **[Story] labels**: Map tasks to specific user stories for traceability
- **TDD Red-Green-Refactor**: MANDATORY per Constitution Principle III. Write tests FIRST (RED), implement minimum code (GREEN), refactor for quality (REFACTOR)
- **Independent stories**: Each user story delivers standalone value and can be tested/deployed independently
- **Checkpoints**: Explicit validation points after Foundational and after each user story
- **Equipment simulator**: Critical tool (T078) for testing without real equipment hardware
- Commit after each Red-Green-Refactor cycle or logical task group
- Stop at any checkpoint to validate story independently before proceeding

---

**Total Tasks**: 79 tasks
**MVP Scope** (User Story 1 only): Tasks T001-T034 (34 tasks)
**Full Feature Scope** (All Stories + Polish): Tasks T001-T079 (79 tasks)
**Parallel Opportunities**: 38 tasks marked [P] can run in parallel (48% of total tasks)
