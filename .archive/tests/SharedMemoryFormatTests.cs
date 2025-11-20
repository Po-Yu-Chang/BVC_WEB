using System.Text;
using System.Text.Json;
using FluentAssertions;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Service.Tests.Contract;

/// <summary>
/// Contract tests for shared memory JSON format.
/// Validates that equipment JSON matches WebAPI InspectionDataRequest schema.
/// RED PHASE: These tests will fail until we implement JSON serialization.
/// </summary>
public class SharedMemoryFormatTests
{
    [Fact]
    public void SharedMemoryJson_ShouldDeserializeToInspectionRecord()
    {
        // Arrange - Equipment writes this JSON to shared memory
        var json = """
        {
          "rowNo": "ROW001",
          "procName": "Blind Hole Inspection",
          "devName": "AOI-MACHINE-01",
          "userName": "operator01",
          "workClass": "Day",
          "traceCode": "TRACE12345",
          "lotNo": null,
          "paramData": [
            {
              "name": "HoleDiameter",
              "value": "0.35",
              "unit": "mm",
              "status": "Pass"
            }
          ],
          "benchmarks": [
            {
              "name": "HoleDiameter",
              "upperLimit": "0.40",
              "lowerLimit": "0.30",
              "target": "0.35",
              "unit": "mm"
            }
          ],
          "otherData": [
            {
              "key": "Temperature",
              "value": "23.5",
              "type": "number",
              "unit": "°C"
            }
          ],
          "inspectionTime": "2025-11-11T10:30:00Z"
        }
        """;

        // Act - Middleware deserializes JSON from shared memory
        var record = JsonSerializer.Deserialize<InspectionRecord>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert - Verify three-tier structure preserved
        record.Should().NotBeNull();
        record!.RowNo.Should().Be("ROW001");
        record.ProcName.Should().Be("Blind Hole Inspection");
        record.DevName.Should().Be("AOI-MACHINE-01");
        record.UserName.Should().Be("operator01");
        record.WorkClass.Should().Be("Day");
        record.TraceCode.Should().Be("TRACE12345");

        // Verify three-tier data model
        record.ParamData.Should().HaveCount(1);
        record.ParamData[0].Name.Should().Be("HoleDiameter");
        record.ParamData[0].Value.Should().Be("0.35");
        record.ParamData[0].Unit.Should().Be("mm");
        record.ParamData[0].Status.Should().Be("Pass");

        record.Benchmarks.Should().HaveCount(1);
        record.Benchmarks[0].Name.Should().Be("HoleDiameter");
        record.Benchmarks[0].UpperLimit.Should().Be("0.40");
        record.Benchmarks[0].LowerLimit.Should().Be("0.30");

        record.OtherData.Should().HaveCount(1);
        record.OtherData[0].Key.Should().Be("Temperature");
        record.OtherData[0].Value.Should().Be("23.5");
    }

    [Fact]
    public void SharedMemoryJson_WithLotNoInsteadOfTraceCode_ShouldDeserialize()
    {
        // Arrange - Equipment uses LotNo instead of TraceCode
        var json = """
        {
          "rowNo": "ROW002",
          "procName": "AVI Inspection",
          "devName": "AVI-MACHINE-02",
          "userName": "operator02",
          "workClass": "Night",
          "traceCode": null,
          "lotNo": "LOT67890",
          "paramData": [],
          "benchmarks": [],
          "otherData": [],
          "inspectionTime": "2025-11-11T22:30:00Z"
        }
        """;

        // Act
        var record = JsonSerializer.Deserialize<InspectionRecord>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert - LotNo should be populated
        record.Should().NotBeNull();
        record!.LotNo.Should().Be("LOT67890");
        record.TraceCode.Should().BeNull();
    }

    [Fact]
    public void InspectionRecord_ShouldSerializeToJson_PreservingThreeTierStructure()
    {
        // Arrange - Create InspectionRecord
        var record = new InspectionRecord
        {
            RowNo = "ROW003",
            ProcName = "Final Inspection",
            DevName = "INSPECT-01",
            UserName = "qa_user",
            WorkClass = "Morning",
            TraceCode = "TRACE99999",
            ParamData = new List<ParamDataItem>
            {
                new() { Name = "Voltage", Value = "5.0", Unit = "V", Status = "Pass" }
            },
            Benchmarks = new List<BenchmarkItem>
            {
                new() { Name = "Voltage", UpperLimit = "5.5", LowerLimit = "4.5", Unit = "V" }
            },
            OtherData = new List<OtherDataItem>
            {
                new() { Key = "Humidity", Value = "45", Unit = "%", Type = "number" }
            },
            InspectionTime = DateTime.Parse("2025-11-11T08:00:00Z").ToUniversalTime()
        };

        // Act - Serialize to JSON (for WebAPI upload)
        var json = JsonSerializer.Serialize(record, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        // Assert - Verify JSON contains all required fields
        json.Should().Contain("rowNo");
        json.Should().Contain("ROW003");
        json.Should().Contain("paramData");
        json.Should().Contain("benchmarks");
        json.Should().Contain("otherData");
        json.Should().Contain("Voltage");
        json.Should().Contain("Humidity");

        // Verify round-trip serialization
        var deserialized = JsonSerializer.Deserialize<InspectionRecord>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        deserialized.Should().BeEquivalentTo(record, options => options
            .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1)))
            .WhenTypeIs<DateTime>());
    }

    [Fact]
    public void SharedMemoryJson_WithMissingRequiredFields_ShouldDeserializeButFailValidation()
    {
        // Arrange - Equipment sends incomplete JSON (missing userName)
        var json = """
        {
          "rowNo": "ROW004",
          "procName": "Test",
          "devName": "MACHINE-X",
          "workClass": "Day",
          "traceCode": "TRACE001",
          "paramData": [],
          "benchmarks": [],
          "otherData": []
        }
        """;

        // Act - Deserialization should succeed (models are flexible)
        var deserializeAction = () => JsonSerializer.Deserialize<InspectionRecord>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert - Deserialization should throw or return incomplete object
        // (FluentValidation will catch this later)
        deserializeAction.Should().Throw<JsonException>()
            .WithMessage("*userName*"); // Required property missing
    }

    [Fact]
    public void SharedMemoryJson_WithEmptyThreeTierArrays_ShouldStillDeserialize()
    {
        // Arrange - Equipment sends minimal data with empty arrays
        var json = """
        {
          "rowNo": "ROW005",
          "procName": "Quick Check",
          "devName": "QC-MACHINE",
          "userName": "qc_operator",
          "workClass": "Day",
          "traceCode": "QC12345",
          "paramData": [],
          "benchmarks": [],
          "otherData": [],
          "inspectionTime": "2025-11-11T12:00:00Z"
        }
        """;

        // Act
        var record = JsonSerializer.Deserialize<InspectionRecord>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert - Empty arrays are valid (three-tier structure preserved)
        record.Should().NotBeNull();
        record!.ParamData.Should().BeEmpty();
        record.Benchmarks.Should().BeEmpty();
        record.OtherData.Should().BeEmpty();
    }
}
