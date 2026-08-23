using FluentValidation;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    // Draft/Sent/Paid/Overdue - matches InvoiceService.ValidStatuses exactly, the actual server-side
    // enforced set (not guessed from the frontend, unlike Priority/TaskStatus).
    internal static class InvoiceStatusRule
    {
        public static readonly string[] Valid = { "Draft", "Sent", "Paid", "Overdue" };
    }

    public class CreateInvoiceLineItemDtoValidator : AbstractValidator<CreateInvoiceLineItemDto>
    {
        public CreateInvoiceLineItemDtoValidator()
        {
            RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            RuleFor(x => x.Quantity).GreaterThan(0);
            RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        }
    }

    // UpdateInvoiceDto : CreateInvoiceDto (no extra fields) - AbstractValidator<CreateInvoiceDto> already
    // matches instances passed as UpdateInvoiceDto for FluentValidation's own validator lookups only via
    // exact-type IValidator<T> registration, so UpdateInvoiceDto gets its own validator below rather than
    // relying on inheritance, since ValidationActionFilter resolves by the argument's concrete type.
    public class CreateInvoiceDtoValidator : AbstractValidator<CreateInvoiceDto>
    {
        public CreateInvoiceDtoValidator()
        {
            RuleFor(x => x.InvoiceNumber).NotEmpty().MaximumLength(50);
            RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.IssueDate)
                .WithMessage("DueDate must not be before IssueDate");
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleForEach(x => x.LineItems).SetValidator(new CreateInvoiceLineItemDtoValidator());
        }
    }

    public class UpdateInvoiceDtoValidator : AbstractValidator<UpdateInvoiceDto>
    {
        public UpdateInvoiceDtoValidator()
        {
            RuleFor(x => x.InvoiceNumber).NotEmpty().MaximumLength(50);
            RuleFor(x => x.ClientName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.IssueDate)
                .WithMessage("DueDate must not be before IssueDate");
            RuleFor(x => x.Notes).MaximumLength(2000);
            RuleForEach(x => x.LineItems).SetValidator(new CreateInvoiceLineItemDtoValidator());
        }
    }

    public class UpdateInvoiceStatusDtoValidator : AbstractValidator<UpdateInvoiceStatusDto>
    {
        public UpdateInvoiceStatusDtoValidator()
        {
            RuleFor(x => x.Status).Must(s => InvoiceStatusRule.Valid.Contains(s))
                .WithMessage($"Status must be one of: {string.Join(", ", InvoiceStatusRule.Valid)}");
        }
    }

    public class SaveAsTemplateDtoValidator : AbstractValidator<SaveAsTemplateDto>
    {
        public SaveAsTemplateDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ClientName).MaximumLength(200);
        }
    }
}
