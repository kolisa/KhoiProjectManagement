namespace KhoiProjectManagement.Domain
{
    // One card on a sprint retrospective board. Category is WentWell, ToImprove or Action; Action items
    // can be ticked off (IsDone) so follow-ups are visible when the next sprint's retro opens.
    public class RetroItem : BaseEntity
    {
        public int SprintId { get; set; }
        public virtual Sprint Sprint { get; set; } = null!;

        public string Category { get; set; } = "WentWell";
        public string Text { get; set; } = string.Empty;

        public int AuthoredBy { get; set; }
        public virtual User Author { get; set; } = null!;

        public bool IsDone { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
