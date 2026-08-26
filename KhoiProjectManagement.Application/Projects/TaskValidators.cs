using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    // "todo"/"in-progress"/"blocked"/"completed" confirmed against the frontend's status badge config
    // in App.js.
    internal static class TaskStatusRule
    {
        public static readonly string[] Valid = { "todo", "in-progress", "blocked", "completed" };
    }

    public class CreateTaskDtoValidator : AbstractValidator<CreateTaskDto>
    {
        public CreateTaskDtoValidator()
        {
            RuleFor(x => x.ProjectId).GreaterThan(0);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(4000);
            RuleFor(x => x.Priority).Must(p => PriorityRule.Valid.Contains(p))
                .WithMessage($"Priority must be one of: {string.Join(", ", PriorityRule.Valid)}");
            RuleFor(x => x.AssignedToId).GreaterThan(0).When(x => x.AssignedToId.HasValue);
        }
    }

    public class UpdateTaskDtoValidator : AbstractValidator<UpdateTaskDto>
    {
        public UpdateTaskDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(4000);
            RuleFor(x => x.Status).Must(s => TaskStatusRule.Valid.Contains(s))
                .WithMessage($"Status must be one of: {string.Join(", ", TaskStatusRule.Valid)}");
            RuleFor(x => x.Priority).Must(p => PriorityRule.Valid.Contains(p))
                .WithMessage($"Priority must be one of: {string.Join(", ", PriorityRule.Valid)}");
            RuleFor(x => x.AssignedToId).GreaterThan(0).When(x => x.AssignedToId.HasValue);
        }
    }
}
