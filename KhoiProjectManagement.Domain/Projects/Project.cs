using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Domain
{
    public class Project : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Status { get; set; } = "active"; // active, inactive, completed

        public string Priority { get; set; } = "medium"; // low, medium, high

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;

        // Lazily created (see ProjectService.EnsureProjectSpaceAsync) home Space for this project's
        // planning docs/collaboration content - null until first use, not at project creation.
        public int? SpaceId { get; set; }
        public virtual Space? Space { get; set; }

        // Navigation properties
        public virtual ICollection<ProjectUser> ProjectUsers { get; set; } = new List<ProjectUser>();
        public virtual ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
        public virtual ICollection<ProjectTag> ProjectTags { get; set; } = new List<ProjectTag>();
        public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
}
