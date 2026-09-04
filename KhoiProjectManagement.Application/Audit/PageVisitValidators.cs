using FluentValidation;

namespace KhoiProjectManagement.Application
{
    public class RecordPageVisitDurationDtoValidator : AbstractValidator<RecordPageVisitDurationDto>
    {
        public RecordPageVisitDurationDtoValidator()
        {
            // 86400 = 24h - generous, but rules out a stuck/misbehaving tab reporting something absurd.
            RuleFor(x => x.DurationSeconds).InclusiveBetween(0, 86400);
        }
    }
}
