using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    // Generic/VaultRoot/VaultCategory/WikiSpace/ProjectSpace - the SpaceType enum values as documented
    // on Space.SpaceType (informational only, never read by the permission resolver, but still worth
    // constraining so a typo doesn't silently create an unrecognized type).
    internal static class SpaceTypeRule
    {
        public static readonly string[] Valid = { "Generic", "VaultRoot", "VaultCategory", "WikiSpace", "ProjectSpace" };
    }

    public class CreateSpaceDtoValidator : AbstractValidator<CreateSpaceDto>
    {
        public CreateSpaceDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(1000);
            RuleFor(x => x.ParentSpaceId).GreaterThan(0).When(x => x.ParentSpaceId.HasValue);
            RuleFor(x => x.SpaceType).Must(t => SpaceTypeRule.Valid.Contains(t))
                .WithMessage($"SpaceType must be one of: {string.Join(", ", SpaceTypeRule.Valid)}");
        }
    }

    public class UpdateSpaceDtoValidator : AbstractValidator<UpdateSpaceDto>
    {
        public UpdateSpaceDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(1000);
        }
    }

    public class SetSpacePermissionDtoValidator : AbstractValidator<SetSpacePermissionDto>
    {
        public SetSpacePermissionDtoValidator()
        {
            RuleFor(x => x.Level).Must(l => l is "Read" or "Write" or "Manage")
                .WithMessage("Level must be one of: Read, Write, Manage");
            RuleFor(x => x)
                .Must(x => (x.RoleId.HasValue) ^ (x.UserId.HasValue))
                .WithMessage("Exactly one of RoleId or UserId must be set")
                .WithName("RoleId");
        }
    }
}
