using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    // "Event"/"Promotion" from the original design (general company events vs. promotion
    // announcements naming a SubjectUserId), plus "Marketing"/"Activation" for that team's campaigns
    // and on-the-ground activations - same free-form-string column, no SubjectUserId requirement.
    internal static class CompanyEventTypeRule
    {
        public static readonly string[] Valid = { "Event", "Promotion", "Marketing", "Activation" };
    }

    public class CreateCompanyEventDtoValidator : AbstractValidator<CreateCompanyEventDto>
    {
        public CreateCompanyEventDtoValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.EventType).Must(t => CompanyEventTypeRule.Valid.Contains(t))
                .WithMessage($"EventType must be one of: {string.Join(", ", CompanyEventTypeRule.Valid)}");
            RuleFor(x => x.SubjectUserId).GreaterThan(0).When(x => x.SubjectUserId.HasValue);
            // A Promotion names who was promoted; a general Event has no subject - enforce the pairing
            // rather than silently accepting a Promotion with nobody named or an Event with one.
            RuleFor(x => x.SubjectUserId)
                .NotNull().When(x => x.EventType == "Promotion")
                .WithMessage("SubjectUserId is required when EventType is Promotion");
        }
    }

    public class SetDateOfBirthDtoValidator : AbstractValidator<SetDateOfBirthDto>
    {
        public SetDateOfBirthDtoValidator()
        {
            RuleFor(x => x.DateOfBirth)
                .LessThan(DateTime.UtcNow).WithMessage("DateOfBirth must be in the past")
                .GreaterThan(DateTime.UtcNow.AddYears(-120)).WithMessage("DateOfBirth is not plausible");
        }
    }
}
