namespace KhoiProjectManagement.Domain
{
    public class OnboardingChecklistItem : BaseEntity
    {
        public int ChecklistId { get; set; }
        public virtual OnboardingChecklist Checklist { get; set; } = null!;

        public string Title { get; set; } = string.Empty;

        public bool IsComplete { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
        public virtual User? Completer { get; set; }

        public string? Notes { get; set; }
    }
}
