using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;
namespace KhoiProjectManagement.Application
{
    public class InvoiceService : IInvoiceService
    {
        private static readonly string[] ValidStatuses = { "Draft", "Sent", "Paid", "Overdue" };

        private readonly IRepository<Invoice> _invoiceRepo;
        private readonly IRepository<InvoiceLineItem> _lineItemRepo;
        private readonly IRepository<InvoiceTemplate> _templateRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public InvoiceService(
            IRepository<Invoice> invoiceRepo,
            IRepository<InvoiceLineItem> lineItemRepo,
            IRepository<InvoiceTemplate> templateRepo,
            IUnitOfWork unitOfWork,
            IConfiguration configuration)
        {
            _invoiceRepo = invoiceRepo;
            _lineItemRepo = lineItemRepo;
            _templateRepo = templateRepo;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<List<InvoiceDto>> GetInvoicesAsync()
        {
            var invoices = await _invoiceRepo.Query()
                .Include(i => i.Creator)
                .Include(i => i.LineItems)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return invoices.Select(MapToDto).ToList();
        }

        public async Task<InvoiceDto?> GetInvoiceByIdAsync(int id)
        {
            var invoice = await _invoiceRepo.Query()
                .Include(i => i.Creator)
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.Id == id);

            return invoice == null ? null : MapToDto(invoice);
        }

        public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceDto dto, int createdBy)
        {
            var invoice = new Invoice
            {
                InvoiceNumber = dto.InvoiceNumber,
                ClientName = dto.ClientName,
                IssueDate = dto.IssueDate,
                DueDate = dto.DueDate,
                Notes = dto.Notes,
                CreatedBy = createdBy,
                LineItems = dto.LineItems.Select(li => new InvoiceLineItem
                {
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice
                }).ToList()
            };

            _invoiceRepo.Add(invoice);
            await _unitOfWork.SaveChangesAsync();

            var saved = await _invoiceRepo.Query()
                .Include(i => i.Creator)
                .Include(i => i.LineItems)
                .FirstAsync(i => i.Id == invoice.Id);

            return MapToDto(saved);
        }

        public async Task<bool> UpdateInvoiceAsync(int id, UpdateInvoiceDto dto)
        {
            var invoice = await _invoiceRepo.Query()
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null)
                return false;

            invoice.InvoiceNumber = dto.InvoiceNumber;
            invoice.ClientName = dto.ClientName;
            invoice.IssueDate = dto.IssueDate;
            invoice.DueDate = dto.DueDate;
            invoice.Notes = dto.Notes;

            _lineItemRepo.RemoveRange(invoice.LineItems);
            invoice.LineItems = dto.LineItems.Select(li => new InvoiceLineItem
            {
                InvoiceId = invoice.Id,
                Description = li.Description,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice
            }).ToList();

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteInvoiceAsync(int id)
        {
            var invoice = await _invoiceRepo.FindAsync(id);
            if (invoice == null)
                return false;

            _invoiceRepo.Remove(invoice);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateStatusAsync(int id, string status)
        {
            if (!ValidStatuses.Contains(status))
                throw new InvalidOperationException($"Invalid status '{status}'. Must be one of: {string.Join(", ", ValidStatuses)}.");

            var invoice = await _invoiceRepo.FindAsync(id);
            if (invoice == null)
                return false;

            invoice.Status = status;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<UploadInvoiceFileResultDto?> UploadFileAsync(int id, IFormFile file)
        {
            var invoice = await _invoiceRepo.FindAsync(id);
            if (invoice == null)
                return null;

            // Suggest saving as a template only the first time a look is uploaded to this invoice, and
            // never for an invoice that was itself created from an existing template (already has one).
            var isFirstUpload = string.IsNullOrEmpty(invoice.StoredFileName);
            var suggestTemplate = isFirstUpload && invoice.CreatedFromTemplateId == null;

            var uploadPath = _configuration["FileUpload:InvoicePath"] ?? "wwwroot/invoice-files";
            var storedFileName = await SaveFileToDiskAsync(file, uploadPath);

            // Replace, don't accumulate - an invoice has at most one attached source document at a time.
            if (!string.IsNullOrEmpty(invoice.StoredFileName))
            {
                var oldPath = Path.Combine(uploadPath, invoice.StoredFileName);
                if (File.Exists(oldPath))
                    File.Delete(oldPath);
            }

            invoice.OriginalFileName = file.FileName;
            invoice.StoredFileName = storedFileName;
            invoice.FileContentType = file.ContentType;
            invoice.FileSize = file.Length;

            await _unitOfWork.SaveChangesAsync();
            return new UploadInvoiceFileResultDto { SuggestSaveAsTemplate = suggestTemplate };
        }

        public async Task<List<InvoiceTemplateDto>> GetTemplatesAsync()
        {
            var templates = await _templateRepo.Query()
                .Include(t => t.Creator)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return templates.Select(MapTemplateToDto).ToList();
        }

        public async Task<InvoiceTemplateDto?> SaveAsTemplateAsync(int invoiceId, SaveAsTemplateDto dto, int createdBy)
        {
            var invoice = await _invoiceRepo.Query().FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null || string.IsNullOrEmpty(invoice.StoredFileName))
                return null;

            var uploadPath = _configuration["FileUpload:InvoicePath"] ?? "wwwroot/invoice-files";
            var templatePath = _configuration["FileUpload:InvoiceTemplatePath"] ?? "wwwroot/invoice-templates";

            // Copy the file rather than referencing the invoice's own StoredFileName - the template
            // must survive the source invoice being edited (re-uploaded) or deleted later.
            var sourcePath = Path.Combine(uploadPath, invoice.StoredFileName);
            if (!File.Exists(sourcePath))
                return null;

            var templateStoredName = $"{Guid.NewGuid()}_{invoice.OriginalFileName}";
            Directory.CreateDirectory(templatePath);
            File.Copy(sourcePath, Path.Combine(templatePath, templateStoredName));

            var template = new InvoiceTemplate
            {
                Name = dto.Name,
                ClientName = dto.ClientName ?? invoice.ClientName,
                OriginalFileName = invoice.OriginalFileName!,
                StoredFileName = templateStoredName,
                ContentType = invoice.FileContentType ?? "application/octet-stream",
                FileSize = invoice.FileSize ?? 0,
                CreatedBy = createdBy
            };

            _templateRepo.Add(template);
            await _unitOfWork.SaveChangesAsync();

            var saved = await _templateRepo.Query().Include(t => t.Creator).FirstAsync(t => t.Id == template.Id);
            return MapTemplateToDto(saved);
        }

        public async Task<InvoiceDto?> CreateFromTemplateAsync(int templateId, CreateInvoiceDto dto, int createdBy)
        {
            var template = await _templateRepo.Query().FirstOrDefaultAsync(t => t.Id == templateId);
            if (template == null)
                return null;

            var templatePath = _configuration["FileUpload:InvoiceTemplatePath"] ?? "wwwroot/invoice-templates";
            var uploadPath = _configuration["FileUpload:InvoicePath"] ?? "wwwroot/invoice-files";

            var sourcePath = Path.Combine(templatePath, template.StoredFileName);
            string? newStoredFileName = null;
            if (File.Exists(sourcePath))
            {
                newStoredFileName = $"{Guid.NewGuid()}_{template.OriginalFileName}";
                Directory.CreateDirectory(uploadPath);
                File.Copy(sourcePath, Path.Combine(uploadPath, newStoredFileName));
            }

            var invoice = new Invoice
            {
                InvoiceNumber = dto.InvoiceNumber,
                ClientName = dto.ClientName,
                IssueDate = dto.IssueDate,
                DueDate = dto.DueDate,
                Notes = dto.Notes,
                CreatedBy = createdBy,
                CreatedFromTemplateId = template.Id,
                OriginalFileName = newStoredFileName != null ? template.OriginalFileName : null,
                StoredFileName = newStoredFileName,
                FileContentType = newStoredFileName != null ? template.ContentType : null,
                FileSize = newStoredFileName != null ? template.FileSize : null,
                LineItems = dto.LineItems.Select(li => new InvoiceLineItem
                {
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice
                }).ToList()
            };

            _invoiceRepo.Add(invoice);
            await _unitOfWork.SaveChangesAsync();

            var saved = await _invoiceRepo.Query()
                .Include(i => i.Creator)
                .Include(i => i.LineItems)
                .FirstAsync(i => i.Id == invoice.Id);

            return MapToDto(saved);
        }

        public async Task<bool> DeleteTemplateAsync(int id)
        {
            var template = await _templateRepo.FindAsync(id);
            if (template == null)
                return false;

            var templatePath = _configuration["FileUpload:InvoiceTemplatePath"] ?? "wwwroot/invoice-templates";
            var filePath = Path.Combine(templatePath, template.StoredFileName);
            if (File.Exists(filePath))
                File.Delete(filePath);

            _templateRepo.Remove(template);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        private static async Task<string> SaveFileToDiskAsync(IFormFile file, string uploadPath)
        {
            var storedFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadPath, storedFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return storedFileName;
        }

        private static InvoiceTemplateDto MapTemplateToDto(InvoiceTemplate t) => new()
        {
            Id = t.Id,
            Name = t.Name,
            ClientName = t.ClientName,
            OriginalFileName = t.OriginalFileName,
            CreatorName = t.Creator?.Name ?? "Unknown",
            CreatedAt = t.CreatedAt
        };

        public async Task<(byte[] Content, string ContentType, string FileName)?> DownloadFileAsync(int id)
        {
            var invoice = await _invoiceRepo.FindAsync(id);
            if (invoice == null || string.IsNullOrEmpty(invoice.StoredFileName))
                return null;

            var uploadPath = _configuration["FileUpload:InvoicePath"] ?? "wwwroot/invoice-files";
            var filePath = Path.Combine(uploadPath, invoice.StoredFileName);
            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllBytesAsync(filePath);
            return (content, invoice.FileContentType ?? "application/octet-stream", invoice.OriginalFileName ?? invoice.StoredFileName);
        }

        private static InvoiceDto MapToDto(Invoice invoice) => new()
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            ClientName = invoice.ClientName,
            Status = invoice.Status,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            Notes = invoice.Notes,
            CreatorName = invoice.Creator?.Name ?? "Unknown",
            CreatedAt = invoice.CreatedAt,
            OriginalFileName = invoice.OriginalFileName,
            LineItems = invoice.LineItems.Select(li => new InvoiceLineItemDto
            {
                Id = li.Id,
                Description = li.Description,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice
            }).ToList()
        };
    }
}
