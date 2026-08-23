using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class LoginRequestDtoValidatorTests
    {
        private readonly LoginRequestDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenEmailAndPasswordProvided_HasNoErrors()
        {
            var result = _validator.TestValidate(new LoginRequestDto { Email = "user@khoitech.africa", Password = "correct-horse" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-an-email")]
        [InlineData("missing-at-sign.com")]
        public void Validate_WhenEmailIsMissingOrMalformed_HasErrorOnEmail(string email)
        {
            var result = _validator.TestValidate(new LoginRequestDto { Email = email, Password = "whatever" });
            result.ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Validate_WhenPasswordIsEmpty_HasErrorOnPassword()
        {
            var result = _validator.TestValidate(new LoginRequestDto { Email = "user@khoitech.africa", Password = "" });
            result.ShouldHaveValidationErrorFor(x => x.Password);
        }
    }

    public class CreateProjectDtoValidatorTests
    {
        private readonly CreateProjectDtoValidator _validator = new();

        private static CreateProjectDto ValidDto() => new()
        {
            Name = "Valid Project",
            Description = "A perfectly normal project",
            Priority = "medium",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 2, 1)
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenNameIsEmpty_HasErrorOnName()
        {
            var dto = ValidDto();
            dto.Name = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Validate_WhenNameExceedsMaxLength_HasErrorOnName()
        {
            var dto = ValidDto();
            dto.Name = new string('a', 201);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Validate_WhenNameIsExactlyMaxLength_HasNoErrorOnName()
        {
            var dto = ValidDto();
            dto.Name = new string('a', 200);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Theory]
        [InlineData("urgent")]
        [InlineData("")]
        [InlineData("HIGH")]
        public void Validate_WhenPriorityIsNotOneOfTheAllowedValues_HasErrorOnPriority(string priority)
        {
            var dto = ValidDto();
            dto.Priority = priority;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Priority);
        }

        [Fact]
        public void Validate_WhenEndDateIsBeforeStartDate_HasErrorOnEndDate()
        {
            var dto = ValidDto();
            dto.StartDate = new DateTime(2026, 3, 1);
            dto.EndDate = new DateTime(2026, 2, 1);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.EndDate);
        }

        [Fact]
        public void Validate_WhenTeamMemberIdIsNotPositive_HasErrorOnTeamMemberIds()
        {
            var dto = ValidDto();
            dto.TeamMemberIds = new List<int> { 1, 0, -5 };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("TeamMemberIds[1]");
        }
    }

    public class ResetPasswordRequestDtoValidatorTests
    {
        private readonly ResetPasswordRequestDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenTokenAndPasswordAreValid_HasNoErrors()
        {
            var result = _validator.TestValidate(new ResetPasswordRequestDto { Token = "some-token", NewPassword = "LongEnough1!" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenTokenIsEmpty_HasErrorOnToken()
        {
            var result = _validator.TestValidate(new ResetPasswordRequestDto { Token = "", NewPassword = "LongEnough1!" });
            result.ShouldHaveValidationErrorFor(x => x.Token);
        }

        [Theory]
        [InlineData("")]
        [InlineData("short1")]
        public void Validate_WhenNewPasswordIsEmptyOrTooShort_HasErrorOnNewPassword(string password)
        {
            var result = _validator.TestValidate(new ResetPasswordRequestDto { Token = "some-token", NewPassword = password });
            result.ShouldHaveValidationErrorFor(x => x.NewPassword);
        }
    }
}
