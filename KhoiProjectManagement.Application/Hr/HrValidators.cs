using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public class CreateOnboardingTemplateDtoValidator : AbstractValidator<CreateOnboardingTemplateDto>
    {
        public CreateOnboardingTemplateDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ItemTitles).NotEmpty();
            RuleForEach(x => x.ItemTitles).NotEmpty().MaximumLength(200);
        }
    }

    public class UpdateOnboardingTemplateDtoValidator : AbstractValidator<UpdateOnboardingTemplateDto>
    {
        public UpdateOnboardingTemplateDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleForEach(x => x.ItemTitles).NotEmpty().MaximumLength(200);
        }
    }

    public class CreateOnboardingChecklistDtoValidator : AbstractValidator<CreateOnboardingChecklistDto>
    {
        public CreateOnboardingChecklistDtoValidator()
        {
            RuleFor(x => x.UserId).GreaterThan(0);
            RuleFor(x => x.TemplateId).GreaterThan(0);
        }
    }

    public class UpdateChecklistItemDtoValidator : AbstractValidator<UpdateChecklistItemDto>
    {
        public UpdateChecklistItemDtoValidator()
        {
            RuleFor(x => x.Notes).MaximumLength(1000);
        }
    }
}
