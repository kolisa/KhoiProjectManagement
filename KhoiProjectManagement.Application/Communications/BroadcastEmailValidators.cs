using FluentValidation;

namespace KhoiProjectManagement.Application
{
    public class BroadcastEmailDtoValidator : AbstractValidator<BroadcastEmailDto>
    {
        public BroadcastEmailDtoValidator()
        {
            RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
            RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Select at least one role.");
        }
    }
}
