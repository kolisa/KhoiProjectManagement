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

    public class CreateAdminUserDtoValidator : AbstractValidator<CreateAdminUserDto>
    {
        public CreateAdminUserDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(x => x.Position).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Role).NotEmpty();
            RuleFor(x => x.DateOfBirth)
                .LessThan(DateTime.UtcNow).WithMessage("DateOfBirth must be in the past")
                .GreaterThan(DateTime.UtcNow.AddYears(-120)).WithMessage("DateOfBirth is not plausible")
                .When(x => x.DateOfBirth.HasValue);
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
            RuleFor(x => x.DateOfBirth)
                .LessThan(DateTime.UtcNow).WithMessage("DateOfBirth must be in the past")
                .GreaterThan(DateTime.UtcNow.AddYears(-120)).WithMessage("DateOfBirth is not plausible")
                .When(x => x.DateOfBirth.HasValue);
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
