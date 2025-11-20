using FluentValidation;
using MesMiddleware.Shared.Models;

namespace MesMiddleware.Monitor.Validation;

/// <summary>
/// FluentValidation validator for InspectionRecord.
/// Ensures data completeness before uploading to MES Cloud.
/// </summary>
public class InspectionDataValidator : AbstractValidator<InspectionRecord>
{
    public InspectionDataValidator()
    {
        // Required fields
        RuleFor(x => x.RowNo)
            .NotEmpty()
            .WithMessage("RowNo is required");

        RuleFor(x => x.ProcName)
            .NotEmpty()
            .WithMessage("ProcName (process name) is required");

        RuleFor(x => x.DevName)
            .NotEmpty()
            .WithMessage("DevName (device name) is required");

        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("UserName (operator) is required");

        RuleFor(x => x.WorkClass)
            .NotEmpty()
            .WithMessage("WorkClass (work shift) is required");

        // Either TraceCode OR LotNo must be provided
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.TraceCode) || !string.IsNullOrEmpty(x.LotNo))
            .WithMessage("Either TraceCode or LotNo must be provided for product tracking");

        // Three-tier data structure requirements
        RuleFor(x => x.ParamData)
            .NotNull()
            .WithMessage("ParamData collection is required (can be empty)");

        RuleFor(x => x.Benchmarks)
            .NotNull()
            .WithMessage("Benchmarks collection is required (can be empty)");

        RuleFor(x => x.OtherData)
            .NotNull()
            .WithMessage("OtherData collection is required (can be empty)");

        // InspectionTime validation (允許未來 10 分鐘的誤差，考慮時區和時間同步問題)
        RuleFor(x => x.InspectionTime)
            .LessThanOrEqualTo(DateTime.Now.AddMinutes(10))
            .WithMessage("InspectionTime cannot be more than 10 minutes in the future");

        RuleFor(x => x.InspectionTime)
            .GreaterThan(DateTime.Now.AddYears(-1))
            .WithMessage("InspectionTime cannot be more than 1 year in the past");

        // ParamDataItem validation
        RuleForEach(x => x.ParamData).ChildRules(paramData =>
        {
            paramData.RuleFor(p => p.Name)
                .NotEmpty()
                .WithMessage("ParamData.Name is required");

            paramData.RuleFor(p => p.Value)
                .NotEmpty()
                .WithMessage("ParamData.Value is required");
        });

        // BenchmarkItem validation
        RuleForEach(x => x.Benchmarks).ChildRules(benchmark =>
        {
            benchmark.RuleFor(b => b.Name)
                .NotEmpty()
                .WithMessage("Benchmark.Name is required");
        });

        // OtherDataItem validation
        RuleForEach(x => x.OtherData).ChildRules(otherData =>
        {
            otherData.RuleFor(o => o.Key)
                .NotEmpty()
                .WithMessage("OtherData.Key is required");

            otherData.RuleFor(o => o.Value)
                .NotEmpty()
                .WithMessage("OtherData.Value is required");
        });
    }
}
