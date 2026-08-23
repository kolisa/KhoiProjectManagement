using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public class UpdateNotificationPreferenceDtoValidator : AbstractValidator<UpdateNotificationPreferenceDto>
    {
        public UpdateNotificationPreferenceDtoValidator()
        {
            RuleFor(x => x.NotificationType).NotEmpty().MaximumLength(100);
        }
    }
}
