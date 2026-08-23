using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public class CreateWikiPageDtoValidator : AbstractValidator<CreateWikiPageDto>
    {
        public CreateWikiPageDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
            RuleFor(x => x.SpaceId).GreaterThan(0);
            RuleFor(x => x.ParentPageId).GreaterThan(0).When(x => x.ParentPageId.HasValue);
        }
    }

    public class UpdateWikiPageDtoValidator : AbstractValidator<UpdateWikiPageDto>
    {
        public UpdateWikiPageDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
            RuleFor(x => x.EditSummary).MaximumLength(500);
        }
    }

    public class CreateWikiCommentDtoValidator : AbstractValidator<CreateWikiCommentDto>
    {
        public CreateWikiCommentDtoValidator()
        {
            RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
            RuleFor(x => x.ParentCommentId).GreaterThan(0).When(x => x.ParentCommentId.HasValue);
            RuleFor(x => x.AnchorBlockIndex).GreaterThanOrEqualTo(0).When(x => x.AnchorBlockIndex.HasValue);
        }
    }

    public class MoveWikiPageDtoValidator : AbstractValidator<MoveWikiPageDto>
    {
        public MoveWikiPageDtoValidator()
        {
            RuleFor(x => x.NewParentPageId).GreaterThan(0).When(x => x.NewParentPageId.HasValue);
        }
    }

    public class SetWikiPageLabelsDtoValidator : AbstractValidator<SetWikiPageLabelsDto>
    {
        public SetWikiPageLabelsDtoValidator()
        {
            RuleFor(x => x.Labels).NotNull();
            RuleForEach(x => x.Labels).NotEmpty().MaximumLength(50);
        }
    }

    public class ReorderWikiPagesDtoValidator : AbstractValidator<ReorderWikiPagesDto>
    {
        public ReorderWikiPagesDtoValidator()
        {
            RuleFor(x => x.OrderedPageIds).NotEmpty();
            RuleForEach(x => x.OrderedPageIds).GreaterThan(0);
        }
    }
}
