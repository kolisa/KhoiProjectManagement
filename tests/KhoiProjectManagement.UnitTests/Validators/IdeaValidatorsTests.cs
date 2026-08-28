using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class CreateIdeaDtoValidatorTests
    {
        private readonly CreateIdeaDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenTitleAndDescriptionProvided_HasNoErrors()
        {
            var result = _validator.TestValidate(new CreateIdeaDto { Title = "Dark mode", Description = "Add a dark theme option" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenTitleIsEmpty_HasErrorOnTitle()
        {
            var dto = new CreateIdeaDto { Title = "", Description = "Some description" };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenTitleExceedsMaxLength_HasErrorOnTitle()
        {
            var dto = new CreateIdeaDto { Title = new string('a', 201), Description = "Some description" };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenTitleIsExactlyMaxLength_HasNoErrorOnTitle()
        {
            var dto = new CreateIdeaDto { Title = new string('a', 200), Description = "Some description" };
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenDescriptionIsEmpty_HasErrorOnDescription()
        {
            var dto = new CreateIdeaDto { Title = "Title", Description = "" };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Validate_WhenDescriptionExceedsMaxLength_HasErrorOnDescription()
        {
            var dto = new CreateIdeaDto { Title = "Title", Description = new string('a', 4001) };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }
    }

    public class UpdateIdeaDtoValidatorTests
    {
        private readonly UpdateIdeaDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenTitleAndDescriptionProvided_HasNoErrors()
        {
            var result = _validator.TestValidate(new UpdateIdeaDto { Title = "Dark mode", Description = "Add a dark theme option" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenTitleIsEmpty_HasErrorOnTitle()
        {
            var dto = new UpdateIdeaDto { Title = "", Description = "Some description" };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenTitleExceedsMaxLength_HasErrorOnTitle()
        {
            var dto = new UpdateIdeaDto { Title = new string('a', 201), Description = "Some description" };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Title);
        }

        [Fact]
        public void Validate_WhenDescriptionIsEmpty_HasErrorOnDescription()
        {
            var dto = new UpdateIdeaDto { Title = "Title", Description = "" };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Validate_WhenDescriptionExceedsMaxLength_HasErrorOnDescription()
        {
            var dto = new UpdateIdeaDto { Title = "Title", Description = new string('a', 4001) };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }
    }

    public class IdeaStatusUpdateDtoValidatorTests
    {
        private readonly IdeaStatusUpdateDtoValidator _validator = new();

        [Theory]
        [InlineData("Submitted")]
        [InlineData("UnderReview")]
        [InlineData("Approved")]
        [InlineData("Rejected")]
        [InlineData("ConvertedToProject")]
        public void Validate_WhenStatusIsOneOfTheAllowedValues_HasNoErrors(string status)
        {
            var result = _validator.TestValidate(new IdeaStatusUpdateDto { Status = status });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("Draft")]
        [InlineData("approved")]
        [InlineData("Done")]
        public void Validate_WhenStatusIsNotOneOfTheAllowedValues_HasErrorOnStatus(string status)
        {
            var result = _validator.TestValidate(new IdeaStatusUpdateDto { Status = status });
            result.ShouldHaveValidationErrorFor(x => x.Status);
        }
    }

    public class CreateIdeaCommentDtoValidatorTests
    {
        private readonly CreateIdeaCommentDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenBodyProvided_HasNoErrors()
        {
            var result = _validator.TestValidate(new CreateIdeaCommentDto { Body = "Great idea!" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenBodyIsEmpty_HasErrorOnBody()
        {
            var result = _validator.TestValidate(new CreateIdeaCommentDto { Body = "" });
            result.ShouldHaveValidationErrorFor(x => x.Body);
        }

        [Fact]
        public void Validate_WhenBodyExceedsMaxLength_HasErrorOnBody()
        {
            var result = _validator.TestValidate(new CreateIdeaCommentDto { Body = new string('a', 4001) });
            result.ShouldHaveValidationErrorFor(x => x.Body);
        }
    }

    public class CreateIdeaAttachmentAnnotationDtoValidatorTests
    {
        private readonly CreateIdeaAttachmentAnnotationDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenBodyProvided_HasNoErrors()
        {
            var result = _validator.TestValidate(new CreateIdeaAttachmentAnnotationDto { Body = "Nice mockup" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenBodyIsEmpty_HasErrorOnBody()
        {
            var result = _validator.TestValidate(new CreateIdeaAttachmentAnnotationDto { Body = "" });
            result.ShouldHaveValidationErrorFor(x => x.Body);
        }

        [Fact]
        public void Validate_WhenBodyExceedsMaxLength_HasErrorOnBody()
        {
            var result = _validator.TestValidate(new CreateIdeaAttachmentAnnotationDto { Body = new string('a', 4001) });
            result.ShouldHaveValidationErrorFor(x => x.Body);
        }
    }
}
