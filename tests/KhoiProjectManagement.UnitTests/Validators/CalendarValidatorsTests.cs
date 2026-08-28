using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class CreateCompanyEventDtoValidatorTests
    {
        private readonly CreateCompanyEventDtoValidator _validator = new();

        private static CreateCompanyEventDto ValidEventDto() => new()
        {
            Title = "All-Hands Meeting",
            Description = "Quarterly update",
            EventDate = new DateTime(2026, 9, 1),
            EventType = "Event",
            SubjectUserId = null
        };

        private static CreateCompanyEventDto ValidPromotionDto() => new()
        {
            Title = "Congrats!",
            Description = "Promoted to Senior Engineer",
            EventDate = new DateTime(2026, 9, 1),
            EventType = "Promotion",
            SubjectUserId = 7
        };

        [Fact]
        public void Validate_WhenEventDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidEventDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenPromotionDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidPromotionDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenTitleIsEmpty_HasErrorOnTitle()
        {
            var dto = ValidEventDto();
            dto.Title = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenTitleExceedsMaxLength_HasErrorOnTitle()
        {
            var dto = ValidEventDto();
            dto.Title = new string('a', 201);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenTitleIsExactlyMaxLength_HasNoErrorOnTitle()
        {
            var dto = ValidEventDto();
            dto.Title = new string('a', 200);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenDescriptionExceedsMaxLength_HasErrorOnDescription()
        {
            var dto = ValidEventDto();
            dto.Description = new string('a', 2001);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Holiday")]
        [InlineData("event")] // case-sensitive - lowercase doesn't match the allowed values
        public void Validate_WhenEventTypeIsNotOneOfTheAllowedValues_HasErrorOnEventType(string eventType)
        {
            var dto = ValidEventDto();
            dto.EventType = eventType;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.EventType);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Validate_WhenSubjectUserIdIsProvidedButNotPositive_HasErrorOnSubjectUserId(int subjectUserId)
        {
            var dto = ValidPromotionDto();
            dto.SubjectUserId = subjectUserId;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.SubjectUserId);
        }

        [Fact]
        public void Validate_WhenEventTypeIsPromotionAndSubjectUserIdIsMissing_HasErrorOnSubjectUserId()
        {
            var dto = ValidPromotionDto();
            dto.SubjectUserId = null;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.SubjectUserId);
        }

        [Fact]
        public void Validate_WhenEventTypeIsEventAndSubjectUserIdIsMissing_HasNoErrorOnSubjectUserId()
        {
            var dto = ValidEventDto();
            dto.SubjectUserId = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.SubjectUserId);
        }
    }

    public class SetDateOfBirthDtoValidatorTests
    {
        private readonly SetDateOfBirthDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenDateOfBirthIsAPlausiblePastDate_HasNoErrors()
        {
            var result = _validator.TestValidate(new SetDateOfBirthDto { DateOfBirth = new DateTime(1990, 4, 12) });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenDateOfBirthIsInTheFuture_HasErrorOnDateOfBirth()
        {
            var dto = new SetDateOfBirthDto { DateOfBirth = DateTime.UtcNow.AddDays(1) };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DateOfBirth);
        }

        [Fact]
        public void Validate_WhenDateOfBirthIsMoreThan120YearsAgo_HasErrorOnDateOfBirth()
        {
            var dto = new SetDateOfBirthDto { DateOfBirth = DateTime.UtcNow.AddYears(-121) };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DateOfBirth);
        }
    }
}
