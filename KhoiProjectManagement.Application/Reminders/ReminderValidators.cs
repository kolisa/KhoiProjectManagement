using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    // "InApp"/"Email"/"Both" - only the two channels this app actually has infrastructure for (no
    // SMS/push provider exists anywhere in the codebase).
    internal static class ReminderChannelRule
    {
        public static readonly string[] Valid = { "InApp", "Email", "Both" };
    }

    internal static class RecurrenceTypeRule
    {
        public static readonly string[] Valid = { "Daily", "Weekly", "Monthly" };
    }

    public class CreateReminderDtoValidator : AbstractValidator<CreateReminderDto>
    {
        public CreateReminderDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.Priority).Must(p => PriorityRule.Valid.Contains(p))
                .WithMessage($"Priority must be one of: {string.Join(", ", PriorityRule.Valid)}");
            RuleFor(x => x.Category).MaximumLength(100);
            RuleFor(x => x.Channel).Must(c => ReminderChannelRule.Valid.Contains(c))
                .WithMessage($"Channel must be one of: {string.Join(", ", ReminderChannelRule.Valid)}");
            RuleFor(x => x.AssignedToId).GreaterThan(0).When(x => x.AssignedToId.HasValue);
            RuleFor(x => x.RelatedProjectId).GreaterThan(0).When(x => x.RelatedProjectId.HasValue);
            RuleFor(x => x.RecurrenceType).Must(t => t == null || RecurrenceTypeRule.Valid.Contains(t))
                .WithMessage($"RecurrenceType must be one of: {string.Join(", ", RecurrenceTypeRule.Valid)}");
            RuleFor(x => x.RecurrenceEndDate).GreaterThanOrEqualTo(x => x.DueAt)
                .When(x => x.RecurrenceEndDate.HasValue)
                .WithMessage("RecurrenceEndDate must not be before DueAt");
            RuleFor(x => x.RecurrenceMaxOccurrences).GreaterThan(0).When(x => x.RecurrenceMaxOccurrences.HasValue);
        }
    }

    public class UpdateReminderDtoValidator : AbstractValidator<UpdateReminderDto>
    {
        public UpdateReminderDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.Priority).Must(p => PriorityRule.Valid.Contains(p))
                .WithMessage($"Priority must be one of: {string.Join(", ", PriorityRule.Valid)}");
            RuleFor(x => x.Category).MaximumLength(100);
            RuleFor(x => x.Channel).Must(c => ReminderChannelRule.Valid.Contains(c))
                .WithMessage($"Channel must be one of: {string.Join(", ", ReminderChannelRule.Valid)}");
            RuleFor(x => x.AssignedToId).GreaterThan(0).When(x => x.AssignedToId.HasValue);
            RuleFor(x => x.RelatedProjectId).GreaterThan(0).When(x => x.RelatedProjectId.HasValue);
            RuleFor(x => x.RecurrenceType).Must(t => t == null || RecurrenceTypeRule.Valid.Contains(t))
                .WithMessage($"RecurrenceType must be one of: {string.Join(", ", RecurrenceTypeRule.Valid)}");
            RuleFor(x => x.RecurrenceEndDate).GreaterThanOrEqualTo(x => x.DueAt)
                .When(x => x.RecurrenceEndDate.HasValue)
                .WithMessage("RecurrenceEndDate must not be before DueAt");
            RuleFor(x => x.RecurrenceMaxOccurrences).GreaterThan(0).When(x => x.RecurrenceMaxOccurrences.HasValue);
        }
    }

    public class SnoozeReminderDtoValidator : AbstractValidator<SnoozeReminderDto>
    {
        public SnoozeReminderDtoValidator()
        {
            RuleFor(x => x.SnoozeUntil).GreaterThan(DateTime.UtcNow).WithMessage("SnoozeUntil must be in the future");
        }
    }

    public class BulkReminderActionDtoValidator : AbstractValidator<BulkReminderActionDto>
    {
        public BulkReminderActionDtoValidator()
        {
            RuleFor(x => x.Ids).NotEmpty();
            RuleForEach(x => x.Ids).GreaterThan(0);
        }
    }

    public class BulkRescheduleReminderDtoValidator : AbstractValidator<BulkRescheduleReminderDto>
    {
        public BulkRescheduleReminderDtoValidator()
        {
            RuleFor(x => x.Ids).NotEmpty();
            RuleForEach(x => x.Ids).GreaterThan(0);
        }
    }

    public class BulkPriorityReminderDtoValidator : AbstractValidator<BulkPriorityReminderDto>
    {
        public BulkPriorityReminderDtoValidator()
        {
            RuleFor(x => x.Ids).NotEmpty();
            RuleForEach(x => x.Ids).GreaterThan(0);
            RuleFor(x => x.Priority).Must(p => PriorityRule.Valid.Contains(p))
                .WithMessage($"Priority must be one of: {string.Join(", ", PriorityRule.Valid)}");
        }
    }

    public class BulkAssignReminderDtoValidator : AbstractValidator<BulkAssignReminderDto>
    {
        public BulkAssignReminderDtoValidator()
        {
            RuleFor(x => x.Ids).NotEmpty();
            RuleForEach(x => x.Ids).GreaterThan(0);
            RuleFor(x => x.AssignedToId).GreaterThan(0);
        }
    }
}
