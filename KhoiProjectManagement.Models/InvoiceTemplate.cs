using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // Captures the "look" of an uploaded invoice file (e.g. a client's branded PDF layout) as a
    // reusable starting point - saved as its own file copy (not a reference to the source Invoice's
    // file) so the template survives even if that invoice is later edited or deleted. Line items are
    // deliberately NOT captured here - the ask was specifically to reuse the uploaded document's look,
    // not to template structured billing data (that's what CreateInvoiceDto is for on each new invoice).
    public class InvoiceTemplate
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? ClientName { get; set; }

        [Required]
        [StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
