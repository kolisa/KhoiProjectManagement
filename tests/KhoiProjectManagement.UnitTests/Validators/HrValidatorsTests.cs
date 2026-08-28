using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class CreateOnboardingTemplateDtoValidatorTests
    {
        private readonly CreateOnboardingTemplateDtoValidator _validator = new();

        private static CreateOnboardingTemplateDto ValidDto() => new()
        {
            Name = "Standard Onboarding",
            ItemTitles = new List<string> { "Sign contract", "Laptop setup" }
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

        [Fact]
        public void Validate_WhenItemTitlesIsEmpty_HasErrorOnItemTitles()
        {
            var dto = ValidDto();
            dto.ItemTitles = new List<string>();
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ItemTitles);
        }

        [Fact]
        public void Validate_WhenAnItemTitleIsEmpty_HasErrorOnThatItem()
        {
            var dto = ValidDto();
            dto.ItemTitles = new List<string> { "Sign contract", "" };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("ItemTitles[1]");
        }

        [Fact]
        public void Validate_WhenAnItemTitleExceedsMaxLength_HasErrorOnThatItem()
        {
            var dto = ValidDto();
            dto.ItemTitles = new List<string> { new string('a', 201) };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("ItemTitles[0]");
        }
    }

    public class UpdateOnboardingTemplateDtoValidatorTests
    {
        private readonly UpdateOnboardingTemplateDtoValidator _validator = new();

        private static UpdateOnboardingTemplateDto ValidDto() => new()
        {
            Name = "Standard Onboarding",
            IsActive = true,
            ItemTitles = new List<string> { "Sign contract", "Laptop setup" }
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
        public void Validate_WhenItemTitlesIsEmpty_HasNoErrorOnItemTitles()
        {
            // Unlike Create, Update has no list-level NotEmpty rule on ItemTitles - only RuleForEach,
            // which vacuously passes on an empty list. An empty checklist-item set is a valid edit.
            var dto = ValidDto();
            dto.ItemTitles = new List<string>();
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.ItemTitles);
        }

        [Fact]
        public void Validate_WhenAnItemTitleIsEmpty_HasErrorOnThatItem()
        {
            var dto = ValidDto();
            dto.ItemTitles = new List<string> { "Sign contract", "" };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("ItemTitles[1]");
        }

        [Fact]
        public void Validate_WhenAnItemTitleExceedsMaxLength_HasErrorOnThatItem()
        {
            var dto = ValidDto();
            dto.ItemTitles = new List<string> { new string('a', 201) };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("ItemTitles[0]");
        }
    }

    public class CreateOnboardingChecklistDtoValidatorTests
    {
        private readonly CreateOnboardingChecklistDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenUserIdAndTemplateIdArePositive_HasNoErrors()
        {
            var result = _validator.TestValidate(new CreateOnboardingChecklistDto { UserId = 1, TemplateId = 1 });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Validate_WhenUserIdIsNotPositive_HasErrorOnUserId(int userId)
        {
            var result = _validator.TestValidate(new CreateOnboardingChecklistDto { UserId = userId, TemplateId = 1 });
            result.ShouldHaveValidationErrorFor(x => x.UserId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Validate_WhenTemplateIdIsNotPositive_HasErrorOnTemplateId(int templateId)
        {
            var result = _validator.TestValidate(new CreateOnboardingChecklistDto { UserId = 1, TemplateId = templateId });
            result.ShouldHaveValidationErrorFor(x => x.TemplateId);
        }
    }

    public class UpdateChecklistItemDtoValidatorTests
    {
        private readonly UpdateChecklistItemDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenNotesIsNull_HasNoErrors()
        {
            var result = _validator.TestValidate(new UpdateChecklistItemDto { IsComplete = true, Notes = null });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenNotesIsWithinMaxLength_HasNoErrors()
        {
            var result = _validator.TestValidate(new UpdateChecklistItemDto { IsComplete = false, Notes = "Waiting on IT for laptop." });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenNotesIsExactlyMaxLength_HasNoErrorOnNotes()
        {
            var dto = new UpdateChecklistItemDto { IsComplete = false, Notes = new string('a', 1000) };
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Notes);
        }

        [Fact]
        public void Validate_WhenNotesExceedsMaxLength_HasErrorOnNotes()
        {
            var dto = new UpdateChecklistItemDto { IsComplete = false, Notes = new string('a', 1001) };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Notes);
        }
    }
}
