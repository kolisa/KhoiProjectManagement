using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class CreateReminderDtoValidatorTests
    {
        private readonly CreateReminderDtoValidator _validator = new();

        private static CreateReminderDto ValidDto() => new()
        {
            Title = "Renew SSL certificate",
            Description = "Prod cert expires soon",
            DueAt = new DateTime(2026, 9, 1),
            Priority = "medium",
            Category = "Ops",
            Channel = "InApp"
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenTitleIsEmpty_HasErrorOnTitle()
        {
            var dto = ValidDto();
            dto.Title = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenTitleExceedsMaxLength_HasErrorOnTitle()
        {
            var dto = ValidDto();
            dto.Title = new string('a', 201);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenDescriptionExceedsMaxLength_HasErrorOnDescription()
        {
            var dto = ValidDto();
            dto.Description = new string('a', 2001);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
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

        [Theory]
        [InlineData("low")]
        [InlineData("medium")]
        [InlineData("high")]
        public void Validate_WhenPriorityIsAnAllowedValue_HasNoErrorOnPriority(string priority)
        {
            var dto = ValidDto();
            dto.Priority = priority;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Priority);
        }

        [Fact]
        public void Validate_WhenCategoryExceedsMaxLength_HasErrorOnCategory()
        {
            var dto = ValidDto();
            dto.Category = new string('a', 101);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Category);
        }

        [Theory]
        [InlineData("SMS")]
        [InlineData("")]
        [InlineData("email")] // case-sensitive
        public void Validate_WhenChannelIsNotOneOfTheAllowedValues_HasErrorOnChannel(string channel)
        {
            var dto = ValidDto();
            dto.Channel = channel;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Channel);
        }

        [Theory]
        [InlineData("InApp")]
        [InlineData("Email")]
        [InlineData("Both")]
        public void Validate_WhenChannelIsAnAllowedValue_HasNoErrorOnChannel(string channel)
        {
            var dto = ValidDto();
            dto.Channel = channel;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Channel);
        }

        [Fact]
        public void Validate_WhenAssignedToIdIsZeroOrNegative_HasErrorOnAssignedToId()
        {
            var dto = ValidDto();
            dto.AssignedToId = 0;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AssignedToId);
        }

        [Fact]
        public void Validate_WhenAssignedToIdIsNull_HasNoErrorOnAssignedToId()
        {
            var dto = ValidDto();
            dto.AssignedToId = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.AssignedToId);
        }

        [Fact]
        public void Validate_WhenRelatedProjectIdIsZeroOrNegative_HasErrorOnRelatedProjectId()
        {
            var dto = ValidDto();
            dto.RelatedProjectId = -1;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RelatedProjectId);
        }

        [Theory]
        [InlineData("Yearly")]
        [InlineData("daily")] // case-sensitive
        public void Validate_WhenRecurrenceTypeIsNotRecognized_HasErrorOnRecurrenceType(string recurrenceType)
        {
            var dto = ValidDto();
            dto.RecurrenceType = recurrenceType;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RecurrenceType);
        }

        [Theory]
        [InlineData("Daily")]
        [InlineData("Weekly")]
        [InlineData("Monthly")]
        public void Validate_WhenRecurrenceTypeIsRecognized_HasNoErrorOnRecurrenceType(string recurrenceType)
        {
            var dto = ValidDto();
            dto.RecurrenceType = recurrenceType;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.RecurrenceType);
        }

        [Fact]
        public void Validate_WhenRecurrenceTypeIsNull_HasNoErrorOnRecurrenceType()
        {
            var dto = ValidDto();
            dto.RecurrenceType = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.RecurrenceType);
        }

        [Fact]
        public void Validate_WhenRecurrenceEndDateIsBeforeDueAt_HasErrorOnRecurrenceEndDate()
        {
            var dto = ValidDto();
            dto.DueAt = new DateTime(2026, 9, 10);
            dto.RecurrenceEndDate = new DateTime(2026, 9, 9);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RecurrenceEndDate);
        }

        [Fact]
        public void Validate_WhenRecurrenceEndDateEqualsDueAt_HasNoErrorOnRecurrenceEndDate()
        {
            var dto = ValidDto();
            dto.DueAt = new DateTime(2026, 9, 10);
            dto.RecurrenceEndDate = new DateTime(2026, 9, 10);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.RecurrenceEndDate);
        }

        [Fact]
        public void Validate_WhenRecurrenceEndDateIsAfterDueAt_HasNoErrorOnRecurrenceEndDate()
        {
            var dto = ValidDto();
            dto.DueAt = new DateTime(2026, 9, 10);
            dto.RecurrenceEndDate = new DateTime(2026, 9, 20);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.RecurrenceEndDate);
        }

        [Fact]
        public void Validate_WhenRecurrenceMaxOccurrencesIsZeroOrNegative_HasErrorOnRecurrenceMaxOccurrences()
        {
            var dto = ValidDto();
            dto.RecurrenceMaxOccurrences = 0;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RecurrenceMaxOccurrences);
        }

        [Fact]
        public void Validate_WhenRecurrenceMaxOccurrencesIsPositive_HasNoErrorOnRecurrenceMaxOccurrences()
        {
            var dto = ValidDto();
            dto.RecurrenceMaxOccurrences = 5;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.RecurrenceMaxOccurrences);
        }
    }

    public class UpdateReminderDtoValidatorTests
    {
        private readonly UpdateReminderDtoValidator _validator = new();

        private static UpdateReminderDto ValidDto() => new()
        {
            Title = "Renew SSL certificate",
            DueAt = new DateTime(2026, 9, 1),
            Priority = "medium",
            Channel = "InApp"
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenTitleIsEmpty_HasErrorOnTitle()
        {
            var dto = ValidDto();
            dto.Title = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Theory]
        [InlineData("urgent")]
        [InlineData("")]
        public void Validate_WhenPriorityIsNotOneOfTheAllowedValues_HasErrorOnPriority(string priority)
        {
            var dto = ValidDto();
            dto.Priority = priority;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Priority);
        }

        [Theory]
        [InlineData("SMS")]
        [InlineData("")]
        public void Validate_WhenChannelIsNotOneOfTheAllowedValues_HasErrorOnChannel(string channel)
        {
            var dto = ValidDto();
            dto.Channel = channel;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Channel);
        }

        [Fact]
        public void Validate_WhenRecurrenceTypeIsNotRecognized_HasErrorOnRecurrenceType()
        {
            var dto = ValidDto();
            dto.RecurrenceType = "Fortnightly";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RecurrenceType);
        }

        [Fact]
        public void Validate_WhenRecurrenceEndDateIsBeforeDueAt_HasErrorOnRecurrenceEndDate()
        {
            var dto = ValidDto();
            dto.DueAt = new DateTime(2026, 9, 10);
            dto.RecurrenceEndDate = new DateTime(2026, 9, 1);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RecurrenceEndDate);
        }

        [Fact]
        public void Validate_WhenAssignedToIdIsZeroOrNegative_HasErrorOnAssignedToId()
        {
            var dto = ValidDto();
            dto.AssignedToId = -5;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AssignedToId);
        }

        [Fact]
        public void Validate_WhenRecurrenceMaxOccurrencesIsZeroOrNegative_HasErrorOnRecurrenceMaxOccurrences()
        {
            var dto = ValidDto();
            dto.RecurrenceMaxOccurrences = -1;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.RecurrenceMaxOccurrences);
        }
    }

    public class SnoozeReminderDtoValidatorTests
    {
        private readonly SnoozeReminderDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenSnoozeUntilIsClearlyInTheFuture_HasNoErrors()
        {
            var result = _validator.TestValidate(new SnoozeReminderDto { SnoozeUntil = DateTime.UtcNow.AddDays(5) });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenSnoozeUntilIsInThePast_HasErrorOnSnoozeUntil()
        {
            var result = _validator.TestValidate(new SnoozeReminderDto { SnoozeUntil = DateTime.UtcNow.AddDays(-5) });
            result.ShouldHaveValidationErrorFor(x => x.SnoozeUntil);
        }
    }

    public class BulkReminderActionDtoValidatorTests
    {
        private readonly BulkReminderActionDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenIdsIsNonEmptyAndAllPositive_HasNoErrors()
        {
            var result = _validator.TestValidate(new BulkReminderActionDto { Ids = new List<int> { 1, 2, 3 } });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenIdsIsEmpty_HasErrorOnIds()
        {
            var result = _validator.TestValidate(new BulkReminderActionDto { Ids = new List<int>() });
            result.ShouldHaveValidationErrorFor(x => x.Ids);
        }

        [Fact]
        public void Validate_WhenAnIdIsZeroOrNegative_HasErrorOnThatElement()
        {
            var result = _validator.TestValidate(new BulkReminderActionDto { Ids = new List<int> { 1, 0, -3 } });
            result.ShouldHaveValidationErrorFor("Ids[1]");
            result.ShouldHaveValidationErrorFor("Ids[2]");
        }
    }

    public class BulkRescheduleReminderDtoValidatorTests
    {
        private readonly BulkRescheduleReminderDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenIdsIsNonEmptyAndAllPositive_HasNoErrors()
        {
            var result = _validator.TestValidate(new BulkRescheduleReminderDto { Ids = new List<int> { 1, 2 }, DueAt = DateTime.UtcNow.AddDays(1) });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenIdsIsEmpty_HasErrorOnIds()
        {
            var result = _validator.TestValidate(new BulkRescheduleReminderDto { Ids = new List<int>(), DueAt = DateTime.UtcNow.AddDays(1) });
            result.ShouldHaveValidationErrorFor(x => x.Ids);
        }

        [Fact]
        public void Validate_WhenAnIdIsZeroOrNegative_HasErrorOnThatElement()
        {
            var result = _validator.TestValidate(new BulkRescheduleReminderDto { Ids = new List<int> { 1, -1 }, DueAt = DateTime.UtcNow.AddDays(1) });
            result.ShouldHaveValidationErrorFor("Ids[1]");
        }
    }

    public class BulkPriorityReminderDtoValidatorTests
    {
        private readonly BulkPriorityReminderDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenIdsAreValidAndPriorityIsAllowed_HasNoErrors()
        {
            var result = _validator.TestValidate(new BulkPriorityReminderDto { Ids = new List<int> { 1, 2 }, Priority = "high" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenIdsIsEmpty_HasErrorOnIds()
        {
            var result = _validator.TestValidate(new BulkPriorityReminderDto { Ids = new List<int>(), Priority = "high" });
            result.ShouldHaveValidationErrorFor(x => x.Ids);
        }

        [Theory]
        [InlineData("urgent")]
        [InlineData("")]
        public void Validate_WhenPriorityIsNotOneOfTheAllowedValues_HasErrorOnPriority(string priority)
        {
            var result = _validator.TestValidate(new BulkPriorityReminderDto { Ids = new List<int> { 1 }, Priority = priority });
            result.ShouldHaveValidationErrorFor(x => x.Priority);
        }
    }

    public class BulkAssignReminderDtoValidatorTests
    {
        private readonly BulkAssignReminderDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenIdsAreValidAndAssignedToIdIsPositive_HasNoErrors()
        {
            var result = _validator.TestValidate(new BulkAssignReminderDto { Ids = new List<int> { 1, 2 }, AssignedToId = 5 });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenIdsIsEmpty_HasErrorOnIds()
        {
            var result = _validator.TestValidate(new BulkAssignReminderDto { Ids = new List<int>(), AssignedToId = 5 });
            result.ShouldHaveValidationErrorFor(x => x.Ids);
        }

        [Fact]
        public void Validate_WhenAssignedToIdIsZeroOrNegative_HasErrorOnAssignedToId()
        {
            var result = _validator.TestValidate(new BulkAssignReminderDto { Ids = new List<int> { 1 }, AssignedToId = 0 });
            result.ShouldHaveValidationErrorFor(x => x.AssignedToId);
        }
    }
}
