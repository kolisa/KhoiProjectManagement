using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    // Submitted/UnderReview/Approved/Rejected/ConvertedToProject - matches IdeaService.ValidStatuses.
    internal static class IdeaStatusRule
    {
        public static readonly string[] Valid = { "Submitted", "UnderReview", "Approved", "Rejected", "ConvertedToProject" };
    }

    public class CreateIdeaDtoValidator : AbstractValidator<CreateIdeaDto>
    {
        public CreateIdeaDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        }
    }

    public class UpdateIdeaDtoValidator : AbstractValidator<UpdateIdeaDto>
    {
        public UpdateIdeaDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        }
    }

    public class IdeaStatusUpdateDtoValidator : AbstractValidator<IdeaStatusUpdateDto>
    {
        public IdeaStatusUpdateDtoValidator()
        {
            RuleFor(x => x.Status).Must(s => IdeaStatusRule.Valid.Contains(s))
                .WithMessage($"Status must be one of: {string.Join(", ", IdeaStatusRule.Valid)}");
        }
    }

    public class CreateIdeaCommentDtoValidator : AbstractValidator<CreateIdeaCommentDto>
    {
        public CreateIdeaCommentDtoValidator()
        {
            RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        }
    }

    public class CreateIdeaAttachmentAnnotationDtoValidator : AbstractValidator<CreateIdeaAttachmentAnnotationDto>
    {
        public CreateIdeaAttachmentAnnotationDtoValidator()
        {
            RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        }
    }
}
