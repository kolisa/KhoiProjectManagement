using FluentValidation;

namespace KhoiProjectManagement.Application
{
    public class CreateGroupDtoValidator : AbstractValidator<CreateGroupDto>
    {
        public CreateGroupDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Description).MaximumLength(500);
        }
    }

    public class UpdateGroupDtoValidator : AbstractValidator<UpdateGroupDto>
    {
        public UpdateGroupDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Description).MaximumLength(500);
        }
    }

    public class SetGroupMembersDtoValidator : AbstractValidator<SetGroupMembersDto>
    {
        public SetGroupMembersDtoValidator()
        {
            RuleFor(x => x.UserIds).NotNull();
        }
    }
}
