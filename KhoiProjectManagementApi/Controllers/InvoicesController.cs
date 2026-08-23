using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Flat, record-oriented finance module, most sensitive of the remaining modules - finance.view is
    // required on every action including read, unlike HR's "or self" carve-out (see plan Phase 8.2).
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpGet]
        [Authorize(Policy = "finance.view")]
        public async Task<IActionResult> GetInvoices()
        {
            return Ok(await _invoiceService.GetInvoicesAsync());
        }

        [HttpGet("{id:int}")]
        [Authorize(Policy = "finance.view")]
        public async Task<IActionResult> GetInvoice(int id)
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null)
                return NotFound();

            return Ok(invoice);
        }

        [HttpPost]
        [Authorize(Policy = "finance.manage")]
        public async Task<IActionResult> CreateInvoice(CreateInvoiceDto dto)
        {
            var invoice = await _invoiceService.CreateInvoiceAsync(dto, GetUserId());
            return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
        }

        [HttpPut("{id:int}")]
        [Authorize(Policy = "finance.manage")]
        public async Task<IActionResult> UpdateInvoice(int id, UpdateInvoiceDto dto)
        {
            var updated = await _invoiceService.UpdateInvoiceAsync(id, dto);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "finance.manage")]
        public async Task<IActionResult> DeleteInvoice(int id)
        {
            var deleted = await _invoiceService.DeleteInvoiceAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        [HttpPut("{id}/status")]
        [Authorize(Policy = "finance.manage")]
        public async Task<IActionResult> UpdateStatus(int id, UpdateInvoiceStatusDto dto)
        {
            try
            {
                var updated = await _invoiceService.UpdateStatusAsync(id, dto.Status);
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/upload")]
        [Authorize(Policy = "finance.manage")]
        public async Task<IActionResult> UploadFile(int id, [FromForm] IFormFile file)
        {
            var result = await _invoiceService.UploadFileAsync(id, file);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpGet("{id}/download")]
        [Authorize(Policy = "finance.view")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            var result = await _invoiceService.DownloadFileAsync(id);
            if (result == null)
                return NotFound();

            return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }

        [HttpPost("{id}/save-as-template")]
        [Authorize(Policy = "finance.manage")]
        public async Task<IActionResult> SaveAsTemplate(int id, SaveAsTemplateDto dto)
        {
            var template = await _invoiceService.SaveAsTemplateAsync(id, dto, GetUserId());
            if (template == null)
                return NotFound();

            return CreatedAtAction(nameof(GetTemplates), template);
        }

        [HttpGet("templates")]
        [Authorize(Policy = "finance.view")]
        public async Task<IActionResult> GetTemplates()
        {
            return Ok(await _invoiceService.GetTemplatesAsync());
        }

        [HttpPost("from-template/{templateId}")]
        [Authorize(Policy = "finance.manage")]
        public async Task<IActionResult> CreateFromTemplate(int templateId, CreateInvoiceDto dto)
        {
            var invoice = await _invoiceService.CreateFromTemplateAsync(templateId, dto, GetUserId());
            if (invoice == null)
                return NotFound();

            return CreatedAtAction(nameof(GetInvoice), new { id = invoice.Id }, invoice);
        }

        [HttpDelete("templates/{id}")]
        [Authorize(Policy = "finance.manage")]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            var deleted = await _invoiceService.DeleteTemplateAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            return int.Parse(claim.Value);
        }
    }
}
