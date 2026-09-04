using FluentValidation;

namespace KhoiProjectManagement.Application
{
    public class UpdateSystemOverviewEmailSettingsDtoValidator : AbstractValidator<UpdateSystemOverviewEmailSettingsDto>
    {
        public UpdateSystemOverviewEmailSettingsDtoValidator()
        {
            RuleFor(x => x.DayOfWeek).IsInEnum();
            RuleFor(x => x.Hour).InclusiveBetween(0, 23);
            RuleFor(x => x.Minute).InclusiveBetween(0, 59);
        }
    }
}
