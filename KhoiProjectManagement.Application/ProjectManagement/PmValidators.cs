using FluentValidation;

namespace KhoiProjectManagement.Application
{
    public class SaveWorkItemDtoValidator : AbstractValidator<SaveWorkItemDto>
    {
        public SaveWorkItemDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(20000);   // rich text (HTML)
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

    public class ImportMembersDtoValidator : AbstractValidator<ImportMembersDto>
    {
        public ImportMembersDtoValidator()
        {
            RuleFor(x => x.Rows).NotNull().NotEmpty().WithMessage("There are no rows to import.");
            RuleFor(x => x.Rows.Count).LessThanOrEqualTo(PmProjectService.MaxImportRows)
                .When(x => x.Rows != null).WithMessage($"An import can have at most {PmProjectService.MaxImportRows} rows.");
        }
    }

    public class SetLabelsDtoValidator : AbstractValidator<SetLabelsDto>
    {
        public SetLabelsDtoValidator()
        {
            RuleFor(x => x.Labels).NotNull();
            RuleFor(x => x.Labels.Count).LessThanOrEqualTo(PmLabelService.MaxLabelsPerItem)
                .When(x => x.Labels != null).WithMessage($"An item can have at most {PmLabelService.MaxLabelsPerItem} labels.");
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

namespace KhoiProjectManagement.Application
{
    public class SaveSprintDtoValidator : AbstractValidator<SaveSprintDto>
    {
        public SaveSprintDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Goal).MaximumLength(500);
            RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("End date must be after the start date.");
            RuleFor(x => x.Capacity).InclusiveBetween(0, 1000).When(x => x.Capacity.HasValue);
        }
    }
}
