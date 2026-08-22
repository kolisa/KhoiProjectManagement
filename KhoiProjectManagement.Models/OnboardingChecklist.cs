namespace KhoiProjectManagement.Models
{
    // Instantiated from a Template for one new hire - item titles are copied at creation time
    // (OnboardingChecklistItem.Title), so editing the template later never retroactively rewrites
    // an in-progress checklist.
    public class OnboardingChecklist
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public int TemplateId { get; set; }
        public virtual OnboardingTemplate Template { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public virtual ICollection<OnboardingChecklistItem> Items { get; set; } = new List<OnboardingChecklistItem>();
    }
}
