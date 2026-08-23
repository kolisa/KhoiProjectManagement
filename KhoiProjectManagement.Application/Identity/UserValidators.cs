using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
    {
        public CreateUserDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(x => x.Position).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
            RuleFor(x => x.Role).NotEmpty();
        }
    }

    // Deliberately no Role rule here - UpdateUserProfileDto has no Role field at all (see its own
    // comment: role changes go through the separate, more tightly-guarded AssignUserRolesDto endpoint).
    public class UpdateUserProfileDtoValidator : AbstractValidator<UpdateUserProfileDto>
    {
        public UpdateUserProfileDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(x => x.Position).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Password).MinimumLength(8).When(x => !string.IsNullOrEmpty(x.Password));
        }
    }

    public class AssignUserRolesDtoValidator : AbstractValidator<AssignUserRolesDto>
    {
        public AssignUserRolesDtoValidator()
        {
            RuleFor(x => x.RoleIds).NotNull();
            RuleForEach(x => x.RoleIds).GreaterThan(0);
        }
    }
}
