using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public class SetWidgetAllowlistDtoValidator : AbstractValidator<SetWidgetAllowlistDto>
    {
        public SetWidgetAllowlistDtoValidator()
        {
            RuleFor(x => x.WidgetKey).NotEmpty().MaximumLength(100);
        }
    }

    public class SetWidgetPreferenceDtoValidator : AbstractValidator<SetWidgetPreferenceDto>
    {
        public SetWidgetPreferenceDtoValidator()
        {
            RuleFor(x => x.WidgetKey).NotEmpty().MaximumLength(100);
            RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        }
    }
}
