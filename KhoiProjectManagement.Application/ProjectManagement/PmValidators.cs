using FluentValidation;

namespace KhoiProjectManagement.Application
{
    public class SaveWorkItemDtoValidator : AbstractValidator<SaveWorkItemDto>
    {
        public SaveWorkItemDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(4000);
            RuleFor(x => x.Type).Must(t => WorkItemHierarchy.Types.Contains(t))
                .WithMessage($"Type must be one of: {string.Join(", ", WorkItemHierarchy.Types)}");
            RuleFor(x => x.Status).Must(s => TaskStatusRule.Valid.Contains(s))
                .WithMessage($"Status must be one of: {string.Join(", ", TaskStatusRule.Valid)}");
            RuleFor(x => x.Priority).Must(p => PriorityRule.Valid.Contains(p))
                .WithMessage($"Priority must be one of: {string.Join(", ", PriorityRule.Valid)}");
            RuleFor(x => x.StoryPoints).InclusiveBetween(0, 100).When(x => x.StoryPoints.HasValue);
            RuleFor(x => x.AssignedToId).GreaterThan(0).When(x => x.AssignedToId.HasValue);
            RuleFor(x => x.ParentId).GreaterThan(0).When(x => x.ParentId.HasValue);
        }
    }

    public class SetProjectMemberDtoValidator : AbstractValidator<SetProjectMemberDto>
    {
        public SetProjectMemberDtoValidator()
        {
            RuleFor(x => x.Role).Must(r => ProjectRoles.All.Contains(r))
                .WithMessage($"Role must be one of: {string.Join(", ", ProjectRoles.All)}");
        }
    }
}

namespace KhoiProjectManagement.Application
{
    public class SaveBoardColumnDtoValidator : AbstractValidator<SaveBoardColumnDto>
    {
        public SaveBoardColumnDtoValidator()
        {
            RuleFor(x => x.Status).Must(s => TaskStatusRule.Valid.Contains(s))
                .WithMessage($"Status must be one of: {string.Join(", ", TaskStatusRule.Valid)}");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(40);
            RuleFor(x => x.WipLimit).InclusiveBetween(1, 100).When(x => x.WipLimit.HasValue);
        }
    }

    public class MoveWorkItemDtoValidator : AbstractValidator<MoveWorkItemDto>
    {
        public MoveWorkItemDtoValidator()
        {
            RuleFor(x => x.Status).Must(s => TaskStatusRule.Valid.Contains(s))
                .WithMessage($"Status must be one of: {string.Join(", ", TaskStatusRule.Valid)}");
        }
    }
}
