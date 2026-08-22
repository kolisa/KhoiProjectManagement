using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    public class OnboardingChecklistItem
    {
        public int Id { get; set; }

        public int ChecklistId { get; set; }
        public virtual OnboardingChecklist Checklist { get; set; } = null!;

        [Required]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;

        public bool IsComplete { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
        public virtual User? Completer { get; set; }

        public string? Notes { get; set; }
    }
}
