using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class CreateTimesheetEntryDtoValidatorTests
    {
        private readonly CreateTimesheetEntryDtoValidator _validator = new();

        private static CreateTimesheetEntryDto ValidDto() => new()
        {
            EntryDate = new DateTime(2026, 1, 1),
            ProjectId = 3,
            Description = "Worked on the API",
            Hours = 8m
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenProjectIdIsNull_HasNoErrorOnProjectId()
        {
            var dto = ValidDto();
            dto.ProjectId = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.ProjectId);
        }

        [Fact]
        public void Validate_WhenProjectIdIsNotPositive_HasErrorOnProjectId()
        {
            var dto = ValidDto();
            dto.ProjectId = 0;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ProjectId);
        }

        [Fact]
        public void Validate_WhenDescriptionExceedsMaxLength_HasErrorOnDescription()
        {
            var dto = ValidDto();
            dto.Description = new string('a', 1001);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Validate_WhenDescriptionIsExactlyMaxLength_HasNoErrorOnDescription()
        {
            var dto = ValidDto();
            dto.Description = new string('a', 1000);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(24.01)]
        [InlineData(100)]
        public void Validate_WhenHoursIsZeroOrNegativeOrExceeds24_HasErrorOnHours(double hours)
        {
            var dto = ValidDto();
            dto.Hours = (decimal)hours;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Hours);
        }

        [Theory]
        [InlineData(0.01)]
        [InlineData(8)]
        [InlineData(24)]
        public void Validate_WhenHoursIsWithinValidRange_HasNoErrorOnHours(double hours)
        {
            var dto = ValidDto();
            dto.Hours = (decimal)hours;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Hours);
        }
    }

    public class CreateTimesheetDtoValidatorTests
    {
        private readonly CreateTimesheetDtoValidator _validator = new();

        private static CreateTimesheetDto ValidDto() => new()
        {
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 1, 7),
            Entries = new List<CreateTimesheetEntryDto>
            {
                new() { EntryDate = new DateTime(2026, 1, 1), Hours = 8m }
            }
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenPeriodEndIsBeforePeriodStart_HasErrorOnPeriodEnd()
        {
            var dto = ValidDto();
            dto.PeriodStart = new DateTime(2026, 1, 10);
            dto.PeriodEnd = new DateTime(2026, 1, 1);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.PeriodEnd);
        }

        [Fact]
        public void Validate_WhenPeriodEndEqualsPeriodStart_HasNoErrorOnPeriodEnd()
        {
            var dto = ValidDto();
            dto.PeriodStart = new DateTime(2026, 1, 5);
            dto.PeriodEnd = new DateTime(2026, 1, 5);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.PeriodEnd);
        }

        [Fact]
        public void Validate_WhenAnEntryHasInvalidHours_HasErrorOnThatEntry()
        {
            var dto = ValidDto();
            dto.Entries = new List<CreateTimesheetEntryDto>
            {
                new() { EntryDate = new DateTime(2026, 1, 1), Hours = 0m }
            };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("Entries[0].Hours");
        }
    }

    public class UpdateTimesheetDtoValidatorTests
    {
        private readonly UpdateTimesheetDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenEntriesAreWellFormed_HasNoErrors()
        {
            var dto = new UpdateTimesheetDto
            {
                Entries = new List<CreateTimesheetEntryDto>
                {
                    new() { EntryDate = new DateTime(2026, 1, 1), Hours = 4m }
                }
            };
            _validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenAnEntryHasHoursAboveTwentyFour_HasErrorOnThatEntry()
        {
            var dto = new UpdateTimesheetDto
            {
                Entries = new List<CreateTimesheetEntryDto>
                {
                    new() { EntryDate = new DateTime(2026, 1, 1), Hours = 25m }
                }
            };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("Entries[0].Hours");
        }
    }

    public class RejectTimesheetDtoValidatorTests
    {
        private readonly RejectTimesheetDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenReasonIsProvided_HasNoErrors()
        {
            var result = _validator.TestValidate(new RejectTimesheetDto { Reason = "Missing hours breakdown" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenReasonIsEmpty_HasErrorOnReason()
        {
            var result = _validator.TestValidate(new RejectTimesheetDto { Reason = "" });
            result.ShouldHaveValidationErrorFor(x => x.Reason);
        }

        [Fact]
        public void Validate_WhenReasonExceedsMaxLength_HasErrorOnReason()
        {
            var result = _validator.TestValidate(new RejectTimesheetDto { Reason = new string('a', 1001) });
            result.ShouldHaveValidationErrorFor(x => x.Reason);
        }

        [Fact]
        public void Validate_WhenReasonIsExactlyMaxLength_HasNoErrorOnReason()
        {
            var result = _validator.TestValidate(new RejectTimesheetDto { Reason = new string('a', 1000) });
            result.ShouldNotHaveValidationErrorFor(x => x.Reason);
        }
    }
}
