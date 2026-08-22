using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // Company-wide, flat - not Space-scoped, per the user's explicit choice (see plan Phase 11).
    // Every employee can submit and comment; ideas.manage only gates moving Status and converting.
    public class Idea
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public int SubmittedBy { get; set; }
        public virtual User Submitter { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(20)]
        public string Status { get; set; } = "Submitted";

        public int? ConvertedProjectId { get; set; }
        public virtual Project? ConvertedProject { get; set; }

        public virtual ICollection<IdeaComment> Comments { get; set; } = new List<IdeaComment>();
    }
}
