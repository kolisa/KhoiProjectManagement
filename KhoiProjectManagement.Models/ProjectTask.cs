using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Models
{
    public class ProjectTask
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "todo"; // todo, in-progress, completed

        [Required]
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
