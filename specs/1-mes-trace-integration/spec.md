# Feature Specification: MES Trace Data Integration

**Feature Branch**: `1-mes-trace-integration`
**Created**: 2025-11-10
**Status**: Draft
**Input**: User description: "Implement MES trace data upload integration with Cimforce system for blind hole detection equipment"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Equipment Authentication and Authorization (Priority: P1)

Production equipment (blind hole detection machines, AOI, AVI, VRS, ET machines) needs to securely authenticate with the MES traceability system before uploading any inspection data. Each machine has a unique machine number assigned by the system and must authenticate from its registered IP address.

**Why this priority**: Without authentication, no data can be uploaded to the MES system. This is the foundational capability that enables all other features. Security and data integrity depend on proper equipment authentication.

**Independent Test**: Can be fully tested by configuring equipment with valid machine numbers and IP addresses, attempting login, and verifying token receipt. Delivers secure access credentials for MES integration.

**Acceptance Scenarios**:

1. **Given** equipment with valid machine number (e.g., "S63") and correct IP address, **When** equipment initiates login, **Then** system returns authentication token valid for subsequent operations
2. **Given** equipment with valid machine number but incorrect IP address, **When** equipment attempts login, **Then** system rejects authentication with IP mismatch error message
3. **Given** equipment with invalid machine number, **When** equipment attempts login, **Then** system rejects authentication with machine not found error
4. **Given** authenticated equipment whose IP address changes, **When** equipment attempts data upload, **Then** system requires re-authentication with new IP address
5. **Given** authenticated equipment with long-lived token, **When** equipment performs multiple operations over time, **Then** token remains valid without repeated login

---

### User Story 2 - Inspection Data Upload (Priority: P2)

Production operators need inspection results automatically uploaded to the MES system after each board inspection. The system must capture complete inspection data including summary results, detailed defect information, and test parameters for quality tracking and analysis.

**Why this priority**: Core functionality that enables traceability of production quality data. Once authentication works, this delivers the primary business value of automated data collection and quality monitoring.

**Independent Test**: Can be tested by running inspections on boards with trace codes or lot numbers, capturing defect data, and verifying successful upload to MES system. Delivers complete quality traceability records.

**Acceptance Scenarios**:

1. **Given** authenticated equipment and inspected board with trace code, **When** inspection completes with results, **Then** system uploads complete data package (summary, defects, parameters) to MES
2. **Given** authenticated equipment and inspected board without trace code but with lot number, **When** inspection completes, **Then** system uploads data with lot number as identifier
3. **Given** inspection detecting multiple defects (open circuit, short circuit), **When** data upload occurs, **Then** system includes all defect types with counts, positions, and coordinates
4. **Given** inspection with test parameters and conditions, **When** data upload occurs, **Then** system preserves all test parameters with values and units
5. **Given** inspection on specific board side (TOP/BOT) with machine/manual classification, **When** data upload occurs, **Then** system includes layer information and data source type
6. **Given** complete inspection data ready for upload, **When** MES system is temporarily unavailable, **Then** system queues data and retries upload automatically
7. **Given** upload failure due to missing work order, **When** error occurs, **Then** system displays specific error message to operator for resolution

---

### User Story 3 - Batch Validation (Priority: P3)

Production operators need to verify that trace codes and lot numbers belong to the correct work order to prevent mixed-batch errors. Before or during inspection, the system can validate multiple codes against the active work order.

**Why this priority**: Prevents quality issues from batch mixing but can be implemented after basic data upload works. Enhances quality control but is not required for basic traceability.

**Independent Test**: Can be tested by scanning multiple trace codes for a work order, requesting validation, and verifying correct/incorrect batch identification. Delivers proactive error prevention.

**Acceptance Scenarios**:

1. **Given** active work order and trace codes belonging to that order, **When** operator requests batch validation, **Then** system confirms all codes are valid for the work order
2. **Given** active work order and trace codes from different orders, **When** operator requests batch validation, **Then** system identifies which specific codes do not belong and displays error details
3. **Given** multiple trace codes scanned for validation, **When** validation request sent, **Then** system processes all codes in single request and returns comprehensive results
4. **Given** work order number that does not exist in MES, **When** validation requested, **Then** system returns work order not found error

---

### Edge Cases

- What happens when network connection drops during data upload?
- How does system handle partial inspection data (missing required fields)?
- What happens when trace code is unreadable but lot number is available?
- How does system handle inspection data for equipment types not previously configured?
- What happens when defect count exceeds 99 items per category (documented limit)?
- How does system handle authentication token expiration during long inspection sequences?
- What happens when IP address changes mid-session (DHCP reassignment)?
- How does system handle time zone differences in CheckTime timestamps?
- What happens when operator information (userName, workClass) is missing?

## Requirements *(mandatory)*

### Functional Requirements

#### Authentication & Security

- **FR-001**: System MUST authenticate equipment using machine number and IP address before allowing any data operations
- **FR-002**: System MUST obtain long-lived authentication tokens that remain valid across multiple operations
- **FR-003**: System MUST enforce IP address binding and reject operations from non-matching IP addresses
- **FR-004**: System MUST automatically re-authenticate when IP address or machine name changes
- **FR-005**: System MUST include authentication token in all data upload and validation requests
- **FR-006**: System MUST NOT expose authentication tokens in log files or error messages

#### Data Upload

- **FR-007**: System MUST upload inspection data with all mandatory fields: row number, process name, device name, operator name, work class
- **FR-008**: System MUST include either trace code OR lot number (at minimum one required) for each inspection record
- **FR-009**: System MUST structure inspection data in three tiers: paramData (summary), benchmarks (details), otherData (auxiliary)
- **FR-010**: System MUST follow standardized parameter codes (Result, CheckQty, DefectQty, OkQty, Defect_Qty_01-99, Check_Param_01-99)
- **FR-011**: System MUST include units for all measured values in uploaded data
- **FR-012**: System MUST format timestamps in standardized format for CheckTime field
- **FR-013**: System MUST support uploading multiple equipment types (AOI, AVI, VRS, ET, blind hole detection) with appropriate data mappings
- **FR-014**: System MUST validate all data completeness before upload attempt
- **FR-015**: System MUST handle both machine inspection (SourceType=1) and manual re-inspection (SourceType=2) data

#### Defect Tracking

- **FR-016**: System MUST capture defect summary counts for each defect type identified
- **FR-017**: System MUST record defect positions with X/Y coordinates for spatial analysis
- **FR-018**: System MUST support up to 99 defect categories per inspection (Defect_Qty_01 through Defect_Qty_99)
- **FR-019**: System MUST preserve image file paths associated with defect positions
- **FR-020**: System MUST distinguish between defect types (open circuit, short circuit, etc.) with separate counts

#### Error Handling

- **FR-021**: System MUST validate all server responses for success/failure status codes
- **FR-022**: System MUST capture and log error messages with context (machine number, operation type, timestamp)
- **FR-023**: System MUST display meaningful error messages to operators for resolution
- **FR-024**: System MUST implement retry logic with exponential backoff for transient network failures
- **FR-025**: System MUST queue data locally when MES system is unavailable and retry automatically
- **FR-026**: System MUST preserve data integrity during network interruptions (no partial uploads)

#### Batch Validation

- **FR-027**: System MUST validate trace codes against work orders to detect mixed batches
- **FR-028**: System MUST support validating multiple trace codes in a single validation request
- **FR-029**: System MUST identify specific codes that do not belong to the work order
- **FR-030**: System MUST validate work order existence before processing trace codes

#### Observability

- **FR-031**: System MUST log all MES API interactions (login, upload, validation) with timestamps
- **FR-032**: System MUST track performance metrics: API response times, success rates, upload batch sizes
- **FR-033**: System MUST enable troubleshooting through log analysis without reproducing production issues
- **FR-034**: System MUST record operation context: device info, trace codes, inspection results

### Key Entities

- **Equipment/Machine**: Represents production equipment with unique machine number (PrtMacNo), registered IP address, and authentication token. Binds to specific system user if configured.

- **Authentication Token**: Long-lived credential obtained at login, tied to machine number and IP address, required for all subsequent operations.

- **Inspection Record**: Complete inspection result for a single board/panel including mandatory fields (row number, process name, device name, operator, work class), trace code or lot number, and three-tier data structure.

- **Trace Data Package**: Three-tier structured data containing:
  - paramData: Summary metrics (Result, CheckQty, DefectQty, OkQty)
  - benchmarks: Detailed defect counts, positions, test parameters (up to 99 items per category)
  - otherData: Auxiliary information (layer type, source type, check time)

- **Defect Information**: Specific defect with type classification, count, X/Y coordinates, and associated image file path.

- **Work Order**: Manufacturing order in MES system that groups trace codes/lot numbers for batch validation.

- **Operator Context**: Production operator identification (userName) and shift information (workClass) associated with inspection.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Equipment operators can authenticate production machines in under 5 seconds with valid credentials
- **SC-002**: Inspection data uploads complete within 10 seconds for typical inspection records (10-20 defects)
- **SC-003**: System successfully uploads 95% of inspection records on first attempt under normal network conditions
- **SC-004**: Operators receive clear error messages within 3 seconds when authentication or upload fails
- **SC-005**: System handles network interruptions gracefully with zero data loss (all data eventually uploaded)
- **SC-006**: Batch validation completes within 5 seconds for typical batch sizes (10-50 trace codes)
- **SC-007**: System maintains authentication tokens for at least 8 hours of continuous production without re-authentication
- **SC-008**: 100% of mandatory inspection data fields are captured and uploaded correctly
- **SC-009**: System supports at least 5 different equipment types (AOI, AVI, VRS, ET, blind hole detection) with appropriate data mappings
- **SC-010**: Defect position accuracy preserved to 0.1mm precision in uploaded coordinates
- **SC-011**: Troubleshooting time reduced by 80% through comprehensive audit logs and error context
- **SC-012**: System responds to IP address changes within 10 seconds with automatic re-authentication

### Assumptions

- MES API endpoints are accessible from production equipment network
- Equipment has stable network connectivity (occasional drops acceptable with retry)
- Machine numbers are pre-registered in MES system before equipment deployment
- IP addresses are relatively stable (not changing every few minutes)
- Defect detection logic exists independently and provides structured results
- Image files referenced in defect positions are stored on accessible network paths
- Operators are identified through existing equipment interfaces (badge scan, manual entry, etc.)
- Work order information is available at inspection time for batch validation
- System time on equipment is synchronized for accurate timestamp reporting
- Equipment supports Windows operating system for deployment
