using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public class CreateTimesheetEntryDtoValidator : AbstractValidator<CreateTimesheetEntryDto>
    {
        public CreateTimesheetEntryDtoValidator()
        {
            RuleFor(x => x.ProjectId).GreaterThan(0).When(x => x.ProjectId.HasValue);
            RuleFor(x => x.Description).MaximumLength(1000);
            RuleFor(x => x.Hours).GreaterThan(0).LessThanOrEqualTo(24);
        }
    }

    public class CreateTimesheetDtoValidator : AbstractValidator<CreateTimesheetDto>
    {
        public CreateTimesheetDtoValidator()
        {
            RuleFor(x => x.PeriodEnd).GreaterThanOrEqualTo(x => x.PeriodStart)
                .WithMessage("PeriodEnd must not be before PeriodStart");
            RuleForEach(x => x.Entries).SetValidator(new CreateTimesheetEntryDtoValidator());
        }
    }

    public class UpdateTimesheetDtoValidator : AbstractValidator<UpdateTimesheetDto>
    {
        public UpdateTimesheetDtoValidator()
        {
            RuleForEach(x => x.Entries).SetValidator(new CreateTimesheetEntryDtoValidator());
        }
    }

    public class RejectTimesheetDtoValidator : AbstractValidator<RejectTimesheetDto>
    {
        public RejectTimesheetDtoValidator()
        {
            RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        }
    }
}
