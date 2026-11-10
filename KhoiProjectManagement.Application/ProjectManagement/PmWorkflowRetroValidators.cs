using FluentValidation;

namespace KhoiProjectManagement.Application
{
    public class SaveWorkflowDtoValidator : AbstractValidator<SaveWorkflowDto>
    {
        public SaveWorkflowDtoValidator()
        {
            RuleFor(x => x.Transitions).NotNull().Must(t => t.Count <= 16).WithMessage("Too many transitions.");
        }
    }

    public class SaveRetroItemDtoValidator : AbstractValidator<SaveRetroItemDto>
    {
        public SaveRetroItemDtoValidator()
        {
            RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
            RuleFor(x => x.Category).Must(c => RetroCategories.All.Contains(c))
                .WithMessage("Category must be WentWell, ToImprove or Action.");
        }
    }
}
