using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    public class InvoiceLineItem
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; } = null!;

        [Required]
        [StringLength(300)]
        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
