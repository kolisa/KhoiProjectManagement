using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Domain
{
    public class ProjectTask : BaseEntity
    {
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Status { get; set; } = "todo"; // todo, in-progress, completed

        public string Priority { get; set; } = "medium"; // low, medium, high

        public int? AssignedToId { get; set; }
        public virtual User? AssignedTo { get; set; }

        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public bool IsOverdue => Status != "completed" && DueDate < DateTime.Now;

        // Navigation properties
        public virtual ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();
        public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
}
