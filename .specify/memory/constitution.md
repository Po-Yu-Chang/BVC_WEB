<!--
Sync Impact Report
==================
Version Change: 1.1.0 → 1.1.1
Type: Verification and confirmation - TDD Red-Green-Refactor compliance validated (PATCH)
Last Amendment Date: 2025-11-11

Changes:
- User requested TDD Red-Green-Refactor compliance verification
- Confirmed Principle III already fully implements required TDD cycle
- No content changes needed - existing principles are complete and correct
- Version bump reflects validation activity and date update

Modified Principles:
- None - all principles unchanged

Principles Defined:
- I. API-First Integration
- II. Token-Based Security
- III. Test-First Development (TDD) - Red-Green-Refactor Cycle ✅ VERIFIED COMPLIANT
- IV. Structured Data Contracts
- V. Error Handling & Observability
- VI. Cross-Platform Compatibility

Templates Status:
✅ plan-template.md - Constitution Check section references TDD principle
✅ spec-template.md - Acceptance criteria aligned with testable scenarios
✅ tasks-template.md - Test-first task ordering validates Red-Green-Refactor cycle
✅ checklist-template.md - Compatible with TDD workflow
✅ agent-file-template.md - No TDD-specific requirements

Follow-up Actions:
- None - all templates remain aligned with TDD guidance
- Constitution confirmed ready for TDD Red-Green-Refactor workflow
-->

# 世運盲孔機Web API Constitution

## Core Principles

### I. API-First Integration

Every feature MUST integrate with the Cimforce Trace Management System (思方云追溯系统) following the documented API contracts. All integration points MUST:

- Strictly adhere to the API specification document (CIMFORCE-WPT-20240902)
- Implement device login with token authentication before any data operations
- Use the exact request/response structures defined in the specification
- Handle all documented error codes and response scenarios
- Preserve IP address validation and machine number (PrtMacNo) binding

**Rationale**: This ensures reliable integration with the MES (Manufacturing Execution System) and maintains data traceability across the production line.

### II. Token-Based Security

Authentication and authorization MUST follow the token-based security model:

- Device login MUST obtain a long-lived token via the `/api/prtmac/prtmacuserlogin` endpoint
- All subsequent API requests MUST include the `accessToken` header
- Token refresh MUST occur when IP address or device name changes
- IP address binding (Referrer header) MUST be enforced for all authenticated requests
- Tokens MUST be stored securely and never exposed in logs or error messages

**Rationale**: Ensures secure device-to-MES communication and prevents unauthorized data submission in the manufacturing environment.

### III. Test-First Development (NON-NEGOTIABLE) - Red-Green-Refactor Cycle

The Red-Green-Refactor TDD cycle is mandatory for ALL code. Every feature MUST follow these three phases in order:

#### Phase 1: 🔴 Red (Write Failing Test)

- Write test FIRST before any implementation code
- Test MUST fail initially (compile error or assertion failure)
- Test MUST be reviewed and approved by user/stakeholder before implementation
- Test captures the exact acceptance criteria and API contract
- Verify the test fails for the RIGHT reason (missing functionality, not syntax errors)

#### Phase 2: 🟢 Green (Make Test Pass)

- Implementation proceeds ONLY after failing test is approved
- Write the MINIMUM code necessary to make the test pass
- Focus on making it work, not making it perfect
- Test MUST pass without modification (no changing tests to make code pass)
- All acceptance criteria MUST be satisfied

#### Phase 3: 🔵 Refactor (Improve Code Quality)

- Refactor ONLY with passing tests as safety net
- Improve code structure, readability, and design without changing behavior
- Remove duplication, extract methods, improve naming
- Tests MUST continue passing throughout refactoring
- Commit after each successful refactor cycle

#### TDD Testing Requirements

- **Contract tests** MUST validate exact API request/response formats (paramData, benchmarks, otherData)
- **Integration tests** MUST verify end-to-end data flow with test MES environment
- **Unit tests** MUST cover data transformations, validation logic, and error handling

#### TDD Workflow Example

```
1. 🔴 Write test for device login → Test fails (function not implemented)
2. 🟢 Implement device login → Test passes
3. 🔵 Refactor login code → Tests still pass
4. Commit: "feat: add device login (TDD Red-Green-Refactor)"

Repeat for next feature...
```

**Rationale**: Manufacturing systems require high reliability. The Red-Green-Refactor cycle ensures contract compliance, reduces production defects, and maintains code quality through disciplined incremental development.

### IV. Structured Data Contracts

Data structures MUST follow the three-tier model defined in the API specification:

- **paramData**: Summary data (e.g., Result, CheckQty, DefectQty, OkQty) - MUST use standardized codes
- **benchmarks**: Detailed inspection data (e.g., defect counts, positions, test parameters) - up to 99 items per category
- **otherData**: Auxiliary information (e.g., LayersType, SourceType, CheckTime)

All data submissions MUST:

- Include mandatory fields: rowNo, procName, devName, userName, workClass
- Provide either traceCode OR lotNo (at least one required)
- Follow the recommended parameter code/name conventions (Result, DefectQty, Defect_Qty_01-99, Check_Param_01-99, etc.)
- Use ISO-formatted timestamps for CheckTime
- Preserve unit specifications for all measured values

**Rationale**: Standardized data structures enable consistent reporting, analysis, and integration across different equipment types (AOI, AVI, VRS, ET, blind hole detection).

### V. Error Handling & Observability

Robust error handling and logging MUST be implemented:

- All API responses MUST be validated for success/failure status
- Error messages MUST be captured and logged with context (machine number, operation, timestamp)
- Mixed-batch validation errors (追溯码校验) MUST be clearly reported to operators
- Structured logging MUST include: operation type, device info, trace codes, results
- Performance metrics MUST be tracked: API response times, success rates, batch sizes
- Debugging MUST be possible through log analysis without reproducing production scenarios

**Rationale**: Manufacturing environments require immediate error diagnosis and audit trails for quality control and compliance.

### VI. Cross-Platform Compatibility

Code MUST support Windows deployment with path to .NET Core/Linux expansion:

- Windows-first implementation (primary deployment target)
- .NET technology stack (C#/.NET for Windows services/APIs)
- File system operations MUST use platform-agnostic path handling
- Configuration MUST use standard .NET configuration providers (JSON/environment variables)
- Avoid Windows-specific P/Invoke or COM dependencies where possible
- Document any Windows-specific dependencies for future migration planning

**Rationale**: Current deployment is Windows-based, but modern .NET enables future cross-platform expansion if manufacturing IT infrastructure evolves.

## Development Standards

### API Client Requirements

- Implement retry logic with exponential backoff for transient failures
- Validate all request payloads against API schema before submission
- Cache device tokens appropriately (long-lived but refreshable)
- Implement circuit breaker pattern for MES connectivity issues
- Support batch data submission while respecting API rate limits
- Log all API interactions for troubleshooting and audit purposes

### Data Quality Requirements

- Validate trace codes and lot numbers before submission
- Implement mixed-batch validation via the `/api/transcode/checkcode` endpoint when required
- Support multiple equipment types with appropriate paramData/benchmarks/otherData mappings
- Ensure data completeness before submission (no partial records)
- Preserve data integrity during network interruptions (queue and retry)

### Testing Requirements

Testing MUST cover three layers:

1. **Contract Tests**: Validate exact API request/response structures using JSON schema validation
2. **Integration Tests**: Test against MES test environment or mocks replicating full API behavior
3. **Unit Tests**: Verify data transformation, validation logic, and error handling independently

Focus areas requiring integration tests:

- Device login and token management flows
- Data upload with all three data tiers (paramData, benchmarks, otherData)
- Trace code validation and mixed-batch detection
- Error response handling for all documented failure scenarios
- Network resilience (timeout, retry, connection failures)

## Governance

### Constitution Authority

This constitution supersedes all other development practices and guides all technical decisions. Any deviation MUST be:

1. Documented with explicit justification
2. Reviewed and approved by technical lead
3. Recorded in the Complexity Tracking section of plan.md

### Amendment Process

Constitution amendments require:

- Documentation of the proposed change with rationale
- Review of impact on existing features and templates
- Approval from project stakeholders
- Version increment following semantic versioning (MAJOR.MINOR.PATCH)
- Update of all affected templates and documentation

### Compliance Verification

All code reviews, pull requests, and feature implementations MUST verify:

- API contract compliance (exact request/response formats)
- Token-based authentication implementation
- TDD cycle adherence (tests written first)
- Structured data contract usage (paramData/benchmarks/otherData)
- Error handling and logging coverage
- Cross-platform compatibility considerations

Any added complexity or architectural patterns MUST be justified in the Complexity Tracking section.

### Version Control

**Version**: 1.1.1 | **Ratified**: 2025-11-10 | **Last Amended**: 2025-11-11
