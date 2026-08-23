namespace KhoiProjectManagement.Domain
{
    public class InvoiceLineItem : BaseEntity
    {
        public int InvoiceId { get; set; }
        public virtual Invoice Invoice { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
