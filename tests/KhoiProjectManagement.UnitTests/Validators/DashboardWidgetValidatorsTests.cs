using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class SetWidgetAllowlistDtoValidatorTests
    {
        private readonly SetWidgetAllowlistDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenWidgetKeyIsProvided_HasNoErrors()
        {
            var result = _validator.TestValidate(new SetWidgetAllowlistDto { WidgetKey = "overdue_tasks", IsEnabled = true });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenWidgetKeyIsEmpty_HasErrorOnWidgetKey()
        {
            var result = _validator.TestValidate(new SetWidgetAllowlistDto { WidgetKey = "", IsEnabled = true });
            result.ShouldHaveValidationErrorFor(x => x.WidgetKey);
        }

        [Fact]
        public void Validate_WhenWidgetKeyExceedsMaxLength_HasErrorOnWidgetKey()
        {
            var dto = new SetWidgetAllowlistDto { WidgetKey = new string('a', 101), IsEnabled = true };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.WidgetKey);
        }

        [Fact]
        public void Validate_WhenWidgetKeyIsExactlyMaxLength_HasNoErrorOnWidgetKey()
        {
            var dto = new SetWidgetAllowlistDto { WidgetKey = new string('a', 100), IsEnabled = true };
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.WidgetKey);
        }
    }

    public class SetWidgetPreferenceDtoValidatorTests
    {
        private readonly SetWidgetPreferenceDtoValidator _validator = new();

        private static SetWidgetPreferenceDto ValidDto() => new()
        {
            WidgetKey = "overdue_tasks",
            IsVisible = true,
            SortOrder = 0
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenWidgetKeyIsEmpty_HasErrorOnWidgetKey()
        {
            var dto = ValidDto();
            dto.WidgetKey = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.WidgetKey);
        }

        [Fact]
        public void Validate_WhenWidgetKeyExceedsMaxLength_HasErrorOnWidgetKey()
        {
            var dto = ValidDto();
            dto.WidgetKey = new string('a', 101);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.WidgetKey);
        }

        [Fact]
        public void Validate_WhenWidgetKeyIsExactlyMaxLength_HasNoErrorOnWidgetKey()
        {
            var dto = ValidDto();
            dto.WidgetKey = new string('a', 100);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.WidgetKey);
        }

        [Fact]
        public void Validate_WhenSortOrderIsNegative_HasErrorOnSortOrder()
        {
            var dto = ValidDto();
            dto.SortOrder = -1;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.SortOrder);
        }

        [Fact]
        public void Validate_WhenSortOrderIsZero_HasNoErrorOnSortOrder()
        {
            var dto = ValidDto();
            dto.SortOrder = 0;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.SortOrder);
        }
    }
}
