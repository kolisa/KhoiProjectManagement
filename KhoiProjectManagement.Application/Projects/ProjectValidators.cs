using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    // "low"/"medium"/"high" confirmed against the frontend's PriorityBadge.js color map - the only
    // three values the UI ever renders correctly, so anything else would show up as an unstyled badge.
    internal static class PriorityRule
    {
        public static readonly string[] Valid = { "low", "medium", "high" };
    }

    public class CreateProjectDtoValidator : AbstractValidator<CreateProjectDto>
    {
        public CreateProjectDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(4000);
            RuleFor(x => x.Priority).Must(p => PriorityRule.Valid.Contains(p))
                .WithMessage($"Priority must be one of: {string.Join(", ", PriorityRule.Valid)}");
            RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("EndDate must not be before StartDate");
            RuleForEach(x => x.TeamMemberIds).GreaterThan(0);
        }
    }

    public class UpdateProjectDtoValidator : AbstractValidator<UpdateProjectDto>
    {
        public UpdateProjectDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(4000);
            RuleFor(x => x.Priority).Must(p => PriorityRule.Valid.Contains(p))
                .WithMessage($"Priority must be one of: {string.Join(", ", PriorityRule.Valid)}");
            // Status intentionally left as NotEmpty only, not an allow-list - unlike Priority, the exact
            // set of valid project statuses isn't confirmed anywhere in the frontend, and rejecting a
            // legitimate value we didn't know about would be worse than under-validating this field.
            RuleFor(x => x.Status).NotEmpty();
            RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
                .WithMessage("EndDate must not be before StartDate");
            RuleForEach(x => x.TeamMemberIds).GreaterThan(0);
        }
    }
}
