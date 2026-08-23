using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public class CreateVaultEntryDtoValidator : AbstractValidator<CreateVaultEntryDto>
    {
        public CreateVaultEntryDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.SpaceId).GreaterThan(0);
            RuleFor(x => x.SystemOrUrl).MaximumLength(500);
            RuleFor(x => x.Username).MaximumLength(200);
            RuleFor(x => x.SecretValue).NotEmpty();
            RuleFor(x => x.Notes).MaximumLength(4000);
        }
    }

    public class UpdateVaultEntryDtoValidator : AbstractValidator<UpdateVaultEntryDto>
    {
        public UpdateVaultEntryDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.SystemOrUrl).MaximumLength(500);
            RuleFor(x => x.Username).MaximumLength(200);
            // SecretValue is deliberately optional here (null/empty = "leave unchanged", per its own
            // comment on UpdateVaultEntryDto) - no NotEmpty rule would be correct.
            RuleFor(x => x.Notes).MaximumLength(4000);
        }
    }
}
