# Data Model: MES Trace Data Integration

**Feature**: MES Trace Data Integration
**Date**: 2025-11-10
**Purpose**: Define entities, relationships, validation rules, and state transitions

## Overview

This document defines the data structures required for MES integration, organized by domain concern. The model follows the three-tier data structure (paramData, benchmarks, otherData) mandated by the Cimforce API specification and Constitution Principle IV (Structured Data Contracts).

---

## Entity Catalog

### Core Entities

1. **Equipment** - Production equipment identity and authentication
2. **AuthenticationToken** - Credential for MES API access
3. **InspectionRecord** - Complete inspection result for a single board
4. **TraceDataPackage** - Three-tier structured data (paramData, benchmarks, otherData)
5. **DefectInfo** - Individual defect details with classification and location
6. **ParameterData** - Summary metrics (Result, CheckQty, DefectQty, OkQty)
7. **BenchmarkData** - Detailed inspection data (defect positions, test parameters)
8. **OtherData** - Auxiliary information (layer, source type, timestamp)
9. **BatchValidationRequest** - Work order and trace codes for validation
10. **QueuedUpload** - Offline data pending upload

---

## 1. Equipment

Represents production equipment with unique identity and network configuration.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `MachineNumber` | string | Yes | Pattern: `^[A-Z0-9-]{1,20}$` | Unique machine identifier (e.g., "S63") assigned by MES |
| `IpAddress` | string | Yes | Valid IPv4 format | Registered IP address for authentication |
| `SystemUserId` | string? | No | - | Optional user binding (from MES) |
| `LastAuthenticationTime` | DateTime? | No | UTC | Timestamp of last successful authentication |
| `TokenExpirationTime` | DateTime? | No | UTC | Estimated token expiration (if known) |

### Relationships

- **Has One** `AuthenticationToken` (current active token)
- **Has Many** `InspectionRecord` (historical uploads)

### Validation Rules

- `MachineNumber` MUST match registered value in MES system
- `IpAddress` MUST match caller IP during authentication (enforced by MES API)
- `IpAddress` changes require re-authentication

### Business Rules

- Equipment MUST authenticate before any data operations
- Token obtained during authentication is long-lived (8+ hours typical)
- IP address binding enforced by MES for security

---

## 2. AuthenticationToken

Long-lived credential obtained from MES API login, tied to machine number and IP.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `Token` | string | Yes | Non-empty | Opaque token string (e.g., "autoprtaed02b44469a0e81a63dec431") |
| `MachineNumber` | string | Yes | - | Machine this token belongs to |
| `IpAddress` | string | Yes | Valid IPv4 | IP address token is bound to |
| `IssuedAt` | DateTime | Yes | UTC | Timestamp token was issued |
| `LastUsed` | DateTime? | No | UTC | Timestamp of last API call with this token |
| `IsValid` | bool | Computed | - | False if token known to be expired/revoked |

### Relationships

- **Belongs To** `Equipment`

### Validation Rules

- `Token` MUST NOT be empty or whitespace
- `Token` MUST NOT be logged or exposed in error messages (Constitution II)
- `IpAddress` MUST match equipment's current IP

### Business Rules

- Token MUST be included in `accessToken` header for all authenticated requests
- Token refresh triggered by 401 Unauthorized response or IP change
- Token stored securely (in-memory or encrypted configuration)

---

## 3. InspectionRecord

Complete inspection result for a single board/panel, including mandatory metadata and structured data.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `RowNumber` | int | Yes | > 0 | Data row number, starting from 1 |
| `ProcessName` | string | Yes | Non-empty | Station/process code (e.g., "ET") |
| `DeviceName` | string | Yes | Non-empty | Equipment identifier (e.g., "W4-MXDCJ-001") |
| `UserName` | string | Yes | Non-empty | Operator ID (e.g., "215123") |
| `WorkClass` | string | Yes | Non-empty | Operator shift (e.g., "A", "B", "C") |
| `TraceCode` | string? | Conditional | - | QR code on board (if readable) |
| `LotNumber` | string? | Conditional | - | Lot/batch number (if available) |
| `PartNumber` | string? | No | - | Product model (e.g., "4FD890011A01") |
| `Remark` | string? | No | Max 500 chars | Notes (e.g., board sequence number) |
| `TraceDataPackage` | TraceDataPackage | Yes | Valid structure | Three-tier structured data |
| `InspectionTime` | DateTime | Yes | UTC | Timestamp of inspection completion |
| `UploadStatus` | UploadStatus | Yes | Enum | Pending/Uploaded/Failed |

### Relationships

- **Belongs To** `Equipment`
- **Has One** `TraceDataPackage`
- **References** `WorkOrder` (via LotNumber, external in MES)

### Validation Rules

- **Either** `TraceCode` **OR** `LotNumber` MUST be provided (at least one required per FR-008)
- `RowNumber` MUST be sequential for batch uploads
- All mandatory string fields MUST NOT be empty/whitespace
- `TraceDataPackage` MUST contain valid paramData, benchmarks, otherData

### State Transitions

```
[Created] → [Pending] → [Uploaded]
              ↓
          [Failed] → [Retrying] → [Uploaded]
                                → [Failed]
```

- **Created**: InspectionRecord instantiated after inspection
- **Pending**: Queued for upload (offline or waiting)
- **Uploaded**: Successfully sent to MES
- **Failed**: Upload failed, eligible for retry
- **Retrying**: Retry attempt in progress

---

## 4. TraceDataPackage

Three-tier structured data containing summary, detailed, and auxiliary information per API specification.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `ParamData` | List<ParameterData> | Yes | 1-99 items | Summary metrics (Result, CheckQty, DefectQty, OkQty) |
| `Benchmarks` | List<BenchmarkData> | Yes | 0-99 items | Detailed defects, positions, test parameters |
| `OtherData` | List<OtherData> | Yes | 0-99 items | Auxiliary info (layer, source type, timestamp) |

### Relationships

- **Belongs To** `InspectionRecord`
- **Has Many** `ParameterData`
- **Has Many** `BenchmarkData`
- **Has Many** `OtherData`

### Validation Rules

- `ParamData` MUST contain at least one item (summary required)
- Each list MUST NOT exceed 99 items (API specification limit per FR-018)
- Standard parameter codes MUST be used (Result, CheckQty, DefectQty, OkQty, Defect_Qty_01-99, etc.)

### Business Rules

- Summary metrics (paramData) calculated from detailed defects (benchmarks)
- Benchmark defect positions correlate with summary defect counts
- OtherData provides context for interpreting paramData and benchmarks

---

## 5. ParameterData

Summary-level metrics for inspection result (Result, CheckQty, DefectQty, OkQty).

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `Code` | string | Yes | Standard codes | Parameter code (e.g., "Result", "CheckQty") |
| `Name` | string | Yes | Non-empty | Display name (e.g., "单板检测结果") |
| `Value` | string | Yes | - | Parameter value (e.g., "PASS", "190") |
| `Unit` | string? | No | - | Unit of measurement (e.g., "个", "V") |
| `Description` | string? | No | - | Additional description |

### Standard Codes

| Code | Name | Value Format | Example |
|------|------|--------------|---------|
| `Result` | 单板检测结果 | OK/NG/PASS/FAIL | "PASS" |
| `CheckQty` | 检测数量 | Integer | "190" |
| `DefectQty` | NG/缺陷总数 | Integer | "5" |
| `OkQty` | OK总数 | Integer | "185" |

### Validation Rules

- `Code` SHOULD use standard codes (Result, CheckQty, DefectQty, OkQty)
- `Value` MUST NOT be empty
- Numeric values SHOULD be represented as strings per API spec

---

## 6. BenchmarkData

Detailed inspection data including defect counts, positions, and test parameters.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `Code` | string | Yes | Pattern: `^(Defect_(Qty\|Pos)_\d{2}\|Check_Param_\d{2})$` | Benchmark code (e.g., "Defect_Qty_01") |
| `Name` | string | Yes | Non-empty | Benchmark name (e.g., "开路数量") |
| `Value` | string | Yes | - | Benchmark value (count, coordinates, parameter) |
| `Unit` | string? | No | - | Unit (e.g., "V", "Ω") |
| `Description` | string? | No | - | Additional details (e.g., image path for defect positions) |

### Standard Code Patterns

| Pattern | Usage | Example Code | Example Value |
|---------|-------|--------------|---------------|
| `Defect_Qty_NN` | Defect type count | `Defect_Qty_01` | "5" (5 open circuits) |
| `Defect_Pos_NN` | Defect coordinates | `Defect_Pos_01` | "X=325.0;Y=229.0" |
| `Check_Param_NN` | Test parameter | `Check_Param_01` | "100" (voltage) |

### Validation Rules

- `Code` MUST follow naming convention (Defect_Qty_01-99, Defect_Pos_01-99, Check_Param_01-99)
- Defect position `Value` MUST use format `X={float};Y={float}` per FR-017
- Image paths stored in `Description` field for `Defect_Pos_NN` entries (FR-019)
- Up to 99 items per category (FR-018)

### Business Rules

- Defect counts (Defect_Qty_NN) MUST match summary DefectQty in ParameterData
- Defect positions (Defect_Pos_NN) provide spatial data for defect analysis
- Test parameters (Check_Param_NN) document inspection conditions

---

## 7. OtherData

Auxiliary information providing context for inspection data (layer, source type, timestamp).

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `Code` | string | Yes | Standard codes | Data code (e.g., "LayersType", "SourceType") |
| `Name` | string | Yes | Non-empty | Display name (e.g., "检测面次") |
| `Value` | string | Yes | - | Data value (e.g., "TOP", "1") |
| `Unit` | string? | No | - | Unit (typically empty for categorical data) |
| `Description` | string? | No | - | Additional description |

### Standard Codes

| Code | Name | Value Format | Example | Description |
|------|------|--------------|---------|-------------|
| `LayersType` | 检测面次 | TOP/BOT/L1/L2 | "TOP" | Board side inspected |
| `SourceType` | 数据分类 | 1/2 | "1" | 1=machine inspection, 2=manual re-inspection (FR-015) |
| `CheckTime` | 检测时间 | ISO 8601 | "2024-08-29T08:30:00" | Inspection timestamp (FR-012) |

### Validation Rules

- `Code` SHOULD use standard codes (LayersType, SourceType, CheckTime)
- `CheckTime` value MUST be valid timestamp format
- `SourceType` value MUST be "1" (machine) or "2" (manual re-inspection)

---

## 8. DefectInfo

Individual defect with classification, count, position, and image reference.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `DefectType` | string | Yes | Non-empty | Defect classification (e.g., "开路", "短路") |
| `DefectCode` | string | Yes | Pattern: `^Defect_Qty_\d{2}$` | Code for this defect type (e.g., "Defect_Qty_01") |
| `Count` | int | Yes | ≥ 0 | Number of defects of this type |
| `PositionX` | double? | No | - | X coordinate (mm) |
| `PositionY` | double? | No | - | Y coordinate (mm) |
| `ImagePath` | string? | No | Valid path | Network path to defect image (e.g., "170.18.1.101\\D41029\\0") |

### Relationships

- **Belongs To** `InspectionRecord` (via TraceDataPackage)
- **Mapped To** `BenchmarkData` (count as Defect_Qty_NN, position as Defect_Pos_NN)

### Validation Rules

- `Count` MUST be non-negative
- If `PositionX` provided, `PositionY` MUST also be provided (and vice versa)
- Position precision preserved to 0.1mm per SC-010
- `ImagePath` MUST be accessible network path if provided

### Business Rules

- Up to 99 distinct defect types per inspection (FR-018)
- Defect positions enable spatial analysis (FR-017)
- Image paths preserved for manual review (FR-019)

---

## 9. BatchValidationRequest

Request to validate trace codes against a work order to detect mixed batches.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `WorkOrderType` | int | Yes | Value: 1 | Validation type (fixed value per API spec) |
| `WorkOrderNumber` | string | Yes | Non-empty | Work order identifier (e.g., "229033-0-1-1") |
| `TraceCodes` | List<string> | Yes | 1-100 items | Trace codes to validate |
| `MachineNumber` | string | Yes | - | Requesting equipment identifier |

### Relationships

- **References** `WorkOrder` (external in MES)
- **Submitted By** `Equipment`

### Validation Rules

- `WorkOrderType` MUST always be 1 (per API specification)
- `TraceCodes` list MUST contain at least 1 code
- `TraceCodes` list SHOULD NOT exceed 100 codes (practical limit for single request)
- All trace codes MUST be non-empty strings

### Business Rules

- Validation occurs before or during inspection to prevent mixed-batch errors (FR-027, FR-028)
- MES responds with success if all codes belong to work order
- MES identifies specific codes that don't match (FR-029)

---

## 10. QueuedUpload

Offline data pending upload when MES system is unavailable.

### Properties

| Property | Type | Required | Validation | Description |
|----------|------|----------|------------|-------------|
| `QueueId` | Guid | Yes | Unique | Unique identifier for queued item |
| `FileName` | string | Yes | - | File name in queue directory |
| `InspectionRecord` | InspectionRecord | Yes | Valid | Serialized inspection data |
| `QueuedAt` | DateTime | Yes | UTC | Timestamp added to queue |
| `RetryCount` | int | Yes | ≥ 0 | Number of upload attempts |
| `LastRetryAt` | DateTime? | No | UTC | Timestamp of last retry |
| `Status` | QueueStatus | Yes | Enum | Pending/Processing/Completed/Failed |
| `ErrorMessage` | string? | No | - | Last error details (if failed) |

### Relationships

- **Contains** `InspectionRecord`

### Validation Rules

- `RetryCount` MUST NOT exceed configured max retry count
- `Status` state transitions MUST be valid (see state diagram below)

### State Transitions

```
[Pending] → [Processing] → [Completed]
              ↓
          [Failed] → [Pending] (retry)
```

- **Pending**: Awaiting upload attempt
- **Processing**: Upload in progress
- **Completed**: Successfully uploaded, eligible for archival
- **Failed**: Upload failed, will retry on next cycle

### Business Rules

- Queue persisted as JSON files in `offline-queue/` directory (per research.md R4)
- Failed uploads retried with exponential backoff (5min, 10min, 20min)
- Completed items archived after 7 days (configurable)
- Queue prevents data loss during network interruptions (FR-025, FR-026)

---

## Enumerations

### UploadStatus

```csharp
public enum UploadStatus
{
    Created = 0,      // InspectionRecord instantiated
    Pending = 1,      // Queued for upload
    Uploaded = 2,     // Successfully sent to MES
    Failed = 3,       // Upload failed
    Retrying = 4      // Retry in progress
}
```

### QueueStatus

```csharp
public enum QueueStatus
{
    Pending = 0,      // Awaiting upload
    Processing = 1,   // Upload in progress
    Completed = 2,    // Successfully uploaded
    Failed = 3        // Upload failed, will retry
}
```

### LayerType

```csharp
public enum LayerType
{
    TOP,    // Top side of board
    BOT,    // Bottom side of board
    L1,     // Layer 1 (multi-layer boards)
    L2,     // Layer 2 (multi-layer boards)
    // ... additional layers as needed
}
```

### SourceType

```csharp
public enum SourceType
{
    MachineInspection = 1,    // Automated inspection (FR-015)
    ManualReinspection = 2    // Manual re-inspection
}
```

---

## Entity Relationship Diagram (ERD)

```
┌─────────────┐
│  Equipment  │
├─────────────┤
│ MachineNo   │───────────┬──────────────────┐
│ IpAddress   │           │                  │
└─────────────┘           │                  │
       │                  │                  │
       │ 1                │ 1                │ 1
       ▼                  ▼                  ▼
┌──────────────────┐  ┌─────────────────┐  ┌──────────────┐
│ AuthToken        │  │ InspectionRecord│  │ QueuedUpload │
├──────────────────┤  ├─────────────────┤  ├──────────────┤
│ Token            │  │ RowNumber       │  │ QueueId      │
│ IssuedAt         │  │ ProcessName     │  │ QueuedAt     │
└──────────────────┘  │ TraceCode       │  │ Status       │
                       │ LotNumber       │  └──────────────┘
                       │ UploadStatus    │
                       └─────────────────┘
                              │ 1
                              │
                              ▼ 1
                       ┌─────────────────┐
                       │ TraceDataPackage│
                       ├─────────────────┤
                       │ ParamData[]     │
                       │ Benchmarks[]    │
                       │ OtherData[]     │
                       └─────────────────┘
                              │
              ┌───────────────┼───────────────┐
              │ *             │ *             │ *
              ▼               ▼               ▼
    ┌───────────────┐  ┌──────────────┐  ┌────────────┐
    │ ParameterData │  │ BenchmarkData│  │ OtherData  │
    ├───────────────┤  ├──────────────┤  ├────────────┤
    │ Code          │  │ Code         │  │ Code       │
    │ Name          │  │ Name         │  │ Name       │
    │ Value         │  │ Value        │  │ Value      │
    └───────────────┘  └──────────────┘  └────────────┘
                              │
                              ▼ *
                       ┌──────────────┐
                       │  DefectInfo  │
                       ├──────────────┤
                       │ DefectType   │
                       │ Count        │
                       │ PositionX/Y  │
                       │ ImagePath    │
                       └──────────────┘
```

---

## Data Validation Summary

### Cross-Entity Validation

1. **Equipment ↔ AuthenticationToken**
   - Token IpAddress MUST match Equipment IpAddress
   - Token MachineNumber MUST match Equipment MachineNumber

2. **InspectionRecord ↔ TraceDataPackage**
   - TraceDataPackage ParamData MUST include Result, CheckQty, DefectQty, OkQty
   - TraceDataPackage OtherData MUST include CheckTime, SourceType

3. **ParameterData ↔ BenchmarkData**
   - DefectQty (ParameterData) MUST equal sum of all Defect_Qty_NN (BenchmarkData)
   - Number of Defect_Pos_NN entries SHOULD correlate with DefectQty

4. **QueuedUpload ↔ InspectionRecord**
   - QueuedUpload MUST contain valid InspectionRecord
   - InspectionRecord in queue MUST have UploadStatus = Pending or Failed

---

## Performance Considerations

### Indexing Strategy (if database used in future)

- **Equipment**: Index on `MachineNumber` (primary lookup)
- **InspectionRecord**: Index on `TraceCode`, `LotNumber`, `InspectionTime`
- **QueuedUpload**: Index on `Status`, `QueuedAt` (for retry processing)

### Data Size Estimates

| Entity | Typical Size | Max Size |
|--------|--------------|----------|
| InspectionRecord | 2-5 KB (JSON) | 50 KB (99 defects) |
| TraceDataPackage | 1-3 KB | 30 KB |
| QueuedUpload | Same as InspectionRecord | - |

**Daily Volume** (typical production):
- 100-500 inspections/day per equipment
- 200 KB - 2.5 MB daily data (uncompressed JSON)
- Offline queue: 1-10 MB (10-50 pending uploads during outage)

---

## Next Steps

1. **API Contracts**: Define OpenAPI specifications for authentication, upload, validation endpoints
2. **Implementation**: Generate C# classes from this data model
3. **Validation**: Implement FluentValidation rules for each entity
4. **Testing**: Create unit tests for validation rules and state transitions
