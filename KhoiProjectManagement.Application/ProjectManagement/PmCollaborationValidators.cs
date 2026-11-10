using FluentValidation;

namespace KhoiProjectManagement.Application
{
    public class SaveCommentDtoValidator : AbstractValidator<SaveCommentDto>
    {
        public SaveCommentDtoValidator()
        {
            RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        }
    }

    public class SaveChecklistItemDtoValidator : AbstractValidator<SaveChecklistItemDto>
    {
        public SaveChecklistItemDtoValidator()
        {
            RuleFor(x => x.Text).NotEmpty().MaximumLength(300);
        }
    }

    public class CreateWorkItemLinkDtoValidator : AbstractValidator<CreateWorkItemLinkDto>
    {
        public CreateWorkItemLinkDtoValidator()
        {
            RuleFor(x => x.TargetId).GreaterThan(0);
            RuleFor(x => x.Type).Must(t => WorkItemLinkTypes.All.Contains(t))
                .WithMessage("Link type must be Blocks, Relates or Duplicates.");
        }
    }
}
