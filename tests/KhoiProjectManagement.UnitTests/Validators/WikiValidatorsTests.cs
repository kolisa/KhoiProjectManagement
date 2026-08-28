using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class CreateWikiPageDtoValidatorTests
    {
        private readonly CreateWikiPageDtoValidator _validator = new();

        private static CreateWikiPageDto ValidDto() => new()
        {
            Title = "Onboarding Guide",
            SpaceId = 1,
            ParentPageId = null,
            ContentMarkdown = "# Welcome"
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
            dto.Title = new string('a', 301);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenTitleIsExactlyMaxLength_HasNoErrorOnTitle()
        {
            var dto = ValidDto();
            dto.Title = new string('a', 300);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenSpaceIdIsNotPositive_HasErrorOnSpaceId()
        {
            var dto = ValidDto();
            dto.SpaceId = 0;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.SpaceId);
        }

        [Fact]
        public void Validate_WhenParentPageIdIsZeroOrNegative_HasErrorOnParentPageId()
        {
            var dto = ValidDto();
            dto.ParentPageId = 0;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ParentPageId);
        }

        [Fact]
        public void Validate_WhenParentPageIdIsNull_HasNoErrorOnParentPageId()
        {
            var dto = ValidDto();
            dto.ParentPageId = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.ParentPageId);
        }
    }

    public class UpdateWikiPageDtoValidatorTests
    {
        private readonly UpdateWikiPageDtoValidator _validator = new();

        private static UpdateWikiPageDto ValidDto() => new()
        {
            Title = "Updated Title",
            ContentMarkdown = "Updated content",
            EditSummary = "Fixed a typo"
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
            dto.Title = new string('a', 301);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenEditSummaryExceedsMaxLength_HasErrorOnEditSummary()
        {
            var dto = ValidDto();
            dto.EditSummary = new string('a', 501);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.EditSummary);
        }

        [Fact]
        public void Validate_WhenEditSummaryIsNull_HasNoErrorOnEditSummary()
        {
            var dto = ValidDto();
            dto.EditSummary = null;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.EditSummary);
        }
    }

    public class CreateWikiCommentDtoValidatorTests
    {
        private readonly CreateWikiCommentDtoValidator _validator = new();

        private static CreateWikiCommentDto ValidDto() => new()
        {
            Body = "A perfectly reasonable comment.",
            ParentCommentId = null,
            AnchorBlockIndex = null,
            AnchorText = null
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenBodyIsEmpty_HasErrorOnBody()
        {
            var dto = ValidDto();
            dto.Body = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Body);
        }

        [Fact]
        public void Validate_WhenBodyExceedsMaxLength_HasErrorOnBody()
        {
            var dto = ValidDto();
            dto.Body = new string('a', 4001);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Body);
        }

        [Fact]
        public void Validate_WhenBodyIsExactlyMaxLength_HasNoErrorOnBody()
        {
            var dto = ValidDto();
            dto.Body = new string('a', 4000);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Body);
        }

        [Fact]
        public void Validate_WhenParentCommentIdIsZeroOrNegative_HasErrorOnParentCommentId()
        {
            var dto = ValidDto();
            dto.ParentCommentId = -1;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ParentCommentId);
        }

        [Fact]
        public void Validate_WhenAnchorBlockIndexIsNegative_HasErrorOnAnchorBlockIndex()
        {
            var dto = ValidDto();
            dto.AnchorBlockIndex = -1;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AnchorBlockIndex);
        }

        [Fact]
        public void Validate_WhenAnchorBlockIndexIsZero_HasNoErrorOnAnchorBlockIndex()
        {
            var dto = ValidDto();
            dto.AnchorBlockIndex = 0;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.AnchorBlockIndex);
        }
    }

    public class MoveWikiPageDtoValidatorTests
    {
        private readonly MoveWikiPageDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenNewParentPageIdIsNull_HasNoErrors()
        {
            var result = _validator.TestValidate(new MoveWikiPageDto { NewParentPageId = null });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenNewParentPageIdIsPositive_HasNoErrors()
        {
            var result = _validator.TestValidate(new MoveWikiPageDto { NewParentPageId = 5 });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenNewParentPageIdIsZeroOrNegative_HasErrorOnNewParentPageId()
        {
            var result = _validator.TestValidate(new MoveWikiPageDto { NewParentPageId = 0 });
            result.ShouldHaveValidationErrorFor(x => x.NewParentPageId);
        }
    }

    public class SetWikiPageLabelsDtoValidatorTests
    {
        private readonly SetWikiPageLabelsDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenLabelsIsAnEmptyList_HasNoErrors()
        {
            var result = _validator.TestValidate(new SetWikiPageLabelsDto { Labels = new List<string>() });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenLabelsIsNull_HasErrorOnLabels()
        {
            var result = _validator.TestValidate(new SetWikiPageLabelsDto { Labels = null! });
            result.ShouldHaveValidationErrorFor(x => x.Labels);
        }

        [Fact]
        public void Validate_WhenALabelIsEmpty_HasErrorOnThatLabel()
        {
            var dto = new SetWikiPageLabelsDto { Labels = new List<string> { "valid", "" } };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("Labels[1]");
        }

        [Fact]
        public void Validate_WhenALabelExceedsMaxLength_HasErrorOnThatLabel()
        {
            var dto = new SetWikiPageLabelsDto { Labels = new List<string> { new string('a', 51) } };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("Labels[0]");
        }

        [Fact]
        public void Validate_WhenAllLabelsAreValid_HasNoErrors()
        {
            var dto = new SetWikiPageLabelsDto { Labels = new List<string> { "guides", "onboarding" } };
            _validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();
        }
    }

    public class ReorderWikiPagesDtoValidatorTests
    {
        private readonly ReorderWikiPagesDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenOrderedPageIdsIsEmpty_HasErrorOnOrderedPageIds()
        {
            var result = _validator.TestValidate(new ReorderWikiPagesDto { OrderedPageIds = new List<int>() });
            result.ShouldHaveValidationErrorFor(x => x.OrderedPageIds);
        }

        [Fact]
        public void Validate_WhenAnOrderedPageIdIsNotPositive_HasErrorOnThatEntry()
        {
            var dto = new ReorderWikiPagesDto { OrderedPageIds = new List<int> { 1, 0, -3 } };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("OrderedPageIds[1]");
        }

        [Fact]
        public void Validate_WhenAllOrderedPageIdsArePositive_HasNoErrors()
        {
            var dto = new ReorderWikiPagesDto { OrderedPageIds = new List<int> { 3, 1, 2 } };
            _validator.TestValidate(dto).ShouldNotHaveAnyValidationErrors();
        }
    }
}
