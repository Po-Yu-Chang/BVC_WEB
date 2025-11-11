using FluentAssertions;
using FluentValidation.TestHelper;
using MesMiddleware.Service.Validation;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Tests.Unit;

/// <summary>
/// Unit tests for InspectionDataValidator.
/// Tests FR-009 (validate data completeness before upload).
/// RED PHASE: Test FluentValidation rules for required fields, traceCode OR lotNo validation.
/// </summary>
public class InspectionDataValidatorTests
{
    private readonly InspectionDataValidator _validator;

    public InspectionDataValidatorTests()
    {
        _validator = new InspectionDataValidator();
    }

    [Fact]
    public void Validate_WithAllRequiredFields_ShouldPass()
    {
        // Arrange - FR-009: Required fields: rowNo, procName, devName, userName, workClass, traceCode OR lotNo
        var validRecord = new InspectionRecord
        {
            RowNo = "ROW001",
            ProcName = "Blind Hole Inspection",
            DevName = "AOI-MACHINE-01",
            UserName = "operator01",
            WorkClass = "Day",
            TraceCode = "TRACE12345", // Either TraceCode or LotNo is required
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>(),
            InspectionTime = DateTime.UtcNow
        };

        // Act
        var result = _validator.TestValidate(validRecord);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithMissingRowNo_ShouldFail()
    {
        // Arrange
        var invalidRecord = new InspectionRecord
        {
            RowNo = string.Empty, // Missing required field
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = "operator",
            WorkClass = "Day",
            TraceCode = "TRACE001",
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(invalidRecord);

        // Assert - FR-009: rowNo is required
        result.ShouldHaveValidationErrorFor(x => x.RowNo);
    }

    [Fact]
    public void Validate_WithMissingProcName_ShouldFail()
    {
        // Arrange
        var invalidRecord = new InspectionRecord
        {
            RowNo = "ROW001",
            ProcName = null!, // Missing required field
            DevName = "MACHINE-01",
            UserName = "operator",
            WorkClass = "Day",
            TraceCode = "TRACE001",
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(invalidRecord);

        // Assert - FR-009: procName is required
        result.ShouldHaveValidationErrorFor(x => x.ProcName);
    }

    [Fact]
    public void Validate_WithMissingDevName_ShouldFail()
    {
        // Arrange
        var invalidRecord = new InspectionRecord
        {
            RowNo = "ROW001",
            ProcName = "Test",
            DevName = "", // Missing required field
            UserName = "operator",
            WorkClass = "Day",
            TraceCode = "TRACE001",
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(invalidRecord);

        // Assert - FR-009: devName is required
        result.ShouldHaveValidationErrorFor(x => x.DevName);
    }

    [Fact]
    public void Validate_WithMissingUserName_ShouldFail()
    {
        // Arrange
        var invalidRecord = new InspectionRecord
        {
            RowNo = "ROW001",
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = null!, // Missing required field
            WorkClass = "Day",
            TraceCode = "TRACE001",
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(invalidRecord);

        // Assert - FR-009: userName is required
        result.ShouldHaveValidationErrorFor(x => x.UserName);
    }

    [Fact]
    public void Validate_WithMissingWorkClass_ShouldFail()
    {
        // Arrange
        var invalidRecord = new InspectionRecord
        {
            RowNo = "ROW001",
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = "operator",
            WorkClass = "", // Missing required field
            TraceCode = "TRACE001",
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(invalidRecord);

        // Assert - FR-009: workClass is required
        result.ShouldHaveValidationErrorFor(x => x.WorkClass);
    }

    [Fact]
    public void Validate_WithMissingBothTraceCodeAndLotNo_ShouldFail()
    {
        // Arrange - FR-009: traceCode OR lotNo is required (at least one must be present)
        var invalidRecord = new InspectionRecord
        {
            RowNo = "ROW001",
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = "operator",
            WorkClass = "Day",
            TraceCode = null, // Missing both
            LotNo = null,     // Missing both
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(invalidRecord);

        // Assert - Should fail validation because neither traceCode nor lotNo is present
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("TraceCode") || e.ErrorMessage.Contains("LotNo") || e.ErrorMessage.Contains("tracking"),
            "either TraceCode or LotNo must be present");
    }

    [Fact]
    public void Validate_WithTraceCodeOnly_ShouldPass()
    {
        // Arrange - FR-009: traceCode OR lotNo (TraceCode is present, LotNo is null)
        var validRecord = new InspectionRecord
        {
            RowNo = "ROW001",
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = "operator",
            WorkClass = "Day",
            TraceCode = "TRACE12345", // TraceCode present
            LotNo = null,              // LotNo absent (allowed)
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(validRecord);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithLotNoOnly_ShouldPass()
    {
        // Arrange - FR-009: traceCode OR lotNo (LotNo is present, TraceCode is null)
        var validRecord = new InspectionRecord
        {
            RowNo = "ROW002",
            ProcName = "AVI Inspection",
            DevName = "AVI-MACHINE-02",
            UserName = "operator02",
            WorkClass = "Night",
            TraceCode = null,          // TraceCode absent (allowed)
            LotNo = "LOT67890",        // LotNo present
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(validRecord);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithBothTraceCodeAndLotNo_ShouldPass()
    {
        // Arrange - Having both is also valid
        var validRecord = new InspectionRecord
        {
            RowNo = "ROW003",
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = "operator",
            WorkClass = "Day",
            TraceCode = "TRACE001",  // Both present
            LotNo = "LOT001",        // Both present
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var result = _validator.TestValidate(validRecord);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyThreeTierArrays_ShouldPass()
    {
        // Arrange - Empty arrays are valid (not all inspections have paramData/benchmarks/otherData)
        var validRecord = new InspectionRecord
        {
            RowNo = "ROW004",
            ProcName = "Quick Check",
            DevName = "QC-MACHINE",
            UserName = "qc_operator",
            WorkClass = "Day",
            TraceCode = "QC12345",
            ParamData = new List<ParamDataItem>(),    // Empty is OK
            Benchmarks = new List<BenchmarkItem>(),   // Empty is OK
            OtherData = new List<OtherDataItem>(),    // Empty is OK
            InspectionTime = DateTime.UtcNow
        };

        // Act
        var result = _validator.TestValidate(validRecord);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validator_Integration_WithHostedService_ShouldRejectInvalidData()
    {
        // Arrange - Simulate MiddlewareHostedService validating before upload
        var invalidRecord = new InspectionRecord
        {
            RowNo = "",           // Invalid
            ProcName = "Test",
            DevName = "MACHINE-01",
            UserName = null!,     // Invalid
            WorkClass = "Day",
            TraceCode = null,     // Invalid (both missing)
            LotNo = null,         // Invalid (both missing)
            ParamData = new List<ParamDataItem>(),
            Benchmarks = new List<BenchmarkItem>(),
            OtherData = new List<OtherDataItem>()
        };

        // Act
        var validationResult = await _validator.ValidateAsync(invalidRecord);

        // Assert - FR-009: Middleware must validate data completeness before upload
        validationResult.IsValid.Should().BeFalse("invalid data should not be uploaded");
        validationResult.Errors.Should().HaveCountGreaterThan(0, "multiple validation errors should be detected");

        // Verify specific errors
        validationResult.Errors.Should().Contain(e => e.PropertyName == "RowNo");
        validationResult.Errors.Should().Contain(e => e.PropertyName == "UserName");
    }
}
