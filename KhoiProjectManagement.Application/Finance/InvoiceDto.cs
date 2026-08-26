namespace KhoiProjectManagement.Application
{
    public class InvoiceDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? Notes { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? OriginalFileName { get; set; }
        public List<InvoiceLineItemDto> LineItems { get; set; } = new();

        // Always Sum(Quantity * UnitPrice) over LineItems, computed at read time - never an
        // independently-editable stored field, so it can never drift out of sync.
        public decimal Total => LineItems.Sum(li => li.Quantity * li.UnitPrice);
    }

    public class InvoiceLineItemDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class CreateInvoiceDto
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public string? Notes { get; set; }
        public List<CreateInvoiceLineItemDto> LineItems { get; set; } = new();
    }

    public class CreateInvoiceLineItemDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class UpdateInvoiceDto : CreateInvoiceDto
    {
    }

    public class UpdateInvoiceStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class InvoiceTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ClientName { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SaveAsTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? ClientName { get; set; }
    }

    // Uploading a file to an invoice that doesn't already come from a template returns this hint so
    // the frontend can ask "save this as a reusable template?" right after a successful upload -
    // the prompt only makes sense the first time a given look is uploaded, not on every subsequent
    // invoice made from an existing template.
    public class UploadInvoiceFileResultDto
    {
        public bool SuggestSaveAsTemplate { get; set; }
    }
}
