using FluentValidation.TestHelper;
using KhoiProjectManagement.Application;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Validators
{
    public class CreateInvoiceLineItemDtoValidatorTests
    {
        private readonly CreateInvoiceLineItemDtoValidator _validator = new();

        private static CreateInvoiceLineItemDto ValidDto() => new()
        {
            Description = "Consulting hours",
            Quantity = 2,
            UnitPrice = 100m
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenDescriptionIsEmpty_HasErrorOnDescription()
        {
            var dto = ValidDto();
            dto.Description = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Validate_WhenDescriptionExceedsMaxLength_HasErrorOnDescription()
        {
            var dto = ValidDto();
            dto.Description = new string('a', 501);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WhenQuantityIsZeroOrNegative_HasErrorOnQuantity(decimal quantity)
        {
            var dto = ValidDto();
            dto.Quantity = quantity;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Quantity);
        }

        [Fact]
        public void Validate_WhenUnitPriceIsNegative_HasErrorOnUnitPrice()
        {
            var dto = ValidDto();
            dto.UnitPrice = -0.01m;
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.UnitPrice);
        }

        [Fact]
        public void Validate_WhenUnitPriceIsZero_HasNoErrorOnUnitPrice()
        {
            var dto = ValidDto();
            dto.UnitPrice = 0m;
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.UnitPrice);
        }
    }

    public class CreateInvoiceDtoValidatorTests
    {
        private readonly CreateInvoiceDtoValidator _validator = new();

        private static CreateInvoiceDto ValidDto() => new()
        {
            InvoiceNumber = "INV-1001",
            ClientName = "Acme Co",
            IssueDate = new DateTime(2026, 1, 1),
            DueDate = new DateTime(2026, 1, 31),
            Notes = "Net 30",
            LineItems = new List<CreateInvoiceLineItemDto>
            {
                new() { Description = "Work", Quantity = 1, UnitPrice = 500m }
            }
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenInvoiceNumberIsEmpty_HasErrorOnInvoiceNumber()
        {
            var dto = ValidDto();
            dto.InvoiceNumber = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.InvoiceNumber);
        }

        [Fact]
        public void Validate_WhenClientNameIsEmpty_HasErrorOnClientName()
        {
            var dto = ValidDto();
            dto.ClientName = "";
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ClientName);
        }

        [Fact]
        public void Validate_WhenDueDateIsBeforeIssueDate_HasErrorOnDueDate()
        {
            var dto = ValidDto();
            dto.IssueDate = new DateTime(2026, 3, 1);
            dto.DueDate = new DateTime(2026, 2, 1);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DueDate);
        }

        [Fact]
        public void Validate_WhenDueDateEqualsIssueDate_HasNoErrorOnDueDate()
        {
            var dto = ValidDto();
            dto.IssueDate = new DateTime(2026, 3, 1);
            dto.DueDate = new DateTime(2026, 3, 1);
            _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.DueDate);
        }

        [Fact]
        public void Validate_WhenNotesExceedsMaxLength_HasErrorOnNotes()
        {
            var dto = ValidDto();
            dto.Notes = new string('a', 2001);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Notes);
        }

        [Fact]
        public void Validate_WhenALineItemIsInvalid_HasErrorOnThatLineItemsField()
        {
            var dto = ValidDto();
            dto.LineItems = new List<CreateInvoiceLineItemDto>
            {
                new() { Description = "", Quantity = 1, UnitPrice = 10m }
            };
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor("LineItems[0].Description");
        }
    }

    public class UpdateInvoiceDtoValidatorTests
    {
        private readonly UpdateInvoiceDtoValidator _validator = new();

        private static UpdateInvoiceDto ValidDto() => new()
        {
            InvoiceNumber = "INV-1001",
            ClientName = "Acme Co",
            IssueDate = new DateTime(2026, 1, 1),
            DueDate = new DateTime(2026, 1, 31),
            LineItems = new List<CreateInvoiceLineItemDto>
            {
                new() { Description = "Work", Quantity = 1, UnitPrice = 500m }
            }
        };

        [Fact]
        public void Validate_WhenDtoIsWellFormed_HasNoErrors()
        {
            var result = _validator.TestValidate(ValidDto());
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenDueDateIsBeforeIssueDate_HasErrorOnDueDate()
        {
            var dto = ValidDto();
            dto.IssueDate = new DateTime(2026, 3, 1);
            dto.DueDate = new DateTime(2026, 2, 1);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DueDate);
        }

        [Fact]
        public void Validate_WhenClientNameExceedsMaxLength_HasErrorOnClientName()
        {
            var dto = ValidDto();
            dto.ClientName = new string('a', 201);
            _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ClientName);
        }
    }

    public class UpdateInvoiceStatusDtoValidatorTests
    {
        private readonly UpdateInvoiceStatusDtoValidator _validator = new();

        [Theory]
        [InlineData("Draft")]
        [InlineData("Sent")]
        [InlineData("Paid")]
        [InlineData("Overdue")]
        public void Validate_WhenStatusIsOneOfTheAllowedValues_HasNoErrors(string status)
        {
            var result = _validator.TestValidate(new UpdateInvoiceStatusDto { Status = status });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("Cancelled")]
        [InlineData("paid")]
        public void Validate_WhenStatusIsNotOneOfTheAllowedValues_HasErrorOnStatus(string status)
        {
            var result = _validator.TestValidate(new UpdateInvoiceStatusDto { Status = status });
            result.ShouldHaveValidationErrorFor(x => x.Status);
        }
    }

    public class SaveAsTemplateDtoValidatorTests
    {
        private readonly SaveAsTemplateDtoValidator _validator = new();

        [Fact]
        public void Validate_WhenNameIsProvidedAndClientNameIsOmitted_HasNoErrors()
        {
            var result = _validator.TestValidate(new SaveAsTemplateDto { Name = "Standard Look" });
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void Validate_WhenNameIsEmpty_HasErrorOnName()
        {
            var result = _validator.TestValidate(new SaveAsTemplateDto { Name = "" });
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Validate_WhenNameExceedsMaxLength_HasErrorOnName()
        {
            var result = _validator.TestValidate(new SaveAsTemplateDto { Name = new string('a', 201) });
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Validate_WhenClientNameExceedsMaxLength_HasErrorOnClientName()
        {
            var result = _validator.TestValidate(new SaveAsTemplateDto { Name = "T", ClientName = new string('a', 201) });
            result.ShouldHaveValidationErrorFor(x => x.ClientName);
        }
    }
}
