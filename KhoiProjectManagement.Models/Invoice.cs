using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // Flat, record-oriented, like OnboardingChecklist - no Space needed, and unlike HR there's no
    // natural "or self" carve-out for money (see plan Phase 8). Total is computed from LineItems,
    // never stored, so it can never drift out of sync with them.
    public class Invoice
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(20)]
        public string Status { get; set; } = "Draft";

        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public string? Notes { get; set; }

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Optional: an externally-generated invoice document (e.g. a PDF from another system) attached
        // to this record, alongside the structured LineItems - the two are independent, not mutually
        // exclusive. StoredFileName is GUID-prefixed on disk; OriginalFileName is display-only, exactly
        // the LibraryFile/Attachment convention - disk reads always use StoredFileName, never this.
        [StringLength(255)]
        public string? OriginalFileName { get; set; }

        [StringLength(255)]
        public string? StoredFileName { get; set; }

        [StringLength(100)]
        public string? FileContentType { get; set; }

        public long? FileSize { get; set; }

        // Set when this invoice was created via POST /api/invoices/from-template/{id} - used only to
        // decide whether to suggest "save as template" again on a manual upload (skip the prompt when
        // the file already came from a template).
        public int? CreatedFromTemplateId { get; set; }
        public virtual InvoiceTemplate? CreatedFromTemplate { get; set; }

        public virtual ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();
    }
}
