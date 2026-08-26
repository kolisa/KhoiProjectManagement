using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Http;

namespace KhoiProjectManagement.Application
{
    public interface IInvoiceService
    {
        Task<List<InvoiceDto>> GetInvoicesAsync();
        Task<InvoiceDto?> GetInvoiceByIdAsync(int id);
        Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto dto, int createdBy);
        Task<bool> UpdateInvoiceAsync(int id, UpdateInvoiceDto dto);
        Task<bool> DeleteInvoiceAsync(int id);
        Task<bool> UpdateStatusAsync(int id, string status, int actingUserId);

        // An externally-generated invoice document (e.g. a PDF) attached to the record - independent
        // of the structured LineItems, not a replacement for them. Returns whether the frontend should
        // prompt "save this as a reusable template?" (skipped when the invoice was itself created from
        // an existing template - see InvoiceTemplate).
        Task<UploadInvoiceFileResultDto?> UploadFileAsync(int id, IFormFile file);
        Task<(byte[] Content, string ContentType, string FileName)?> DownloadFileAsync(int id);

        Task<List<InvoiceTemplateDto>> GetTemplatesAsync();
        Task<InvoiceTemplateDto?> SaveAsTemplateAsync(int invoiceId, SaveAsTemplateDto dto, int createdBy);
        Task<InvoiceDto?> CreateFromTemplateAsync(int templateId, CreateInvoiceDto dto, int createdBy);
        Task<bool> DeleteTemplateAsync(int id);
    }
}
