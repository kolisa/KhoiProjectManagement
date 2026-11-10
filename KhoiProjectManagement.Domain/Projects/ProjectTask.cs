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

        public string Type { get; set; } = "Task"; // Task, Meeting, Milestone, Review

        public int? AssignedToId { get; set; }
        public virtual User? AssignedTo { get; set; }

        // Always a full DateTime (not date-only) - a Meeting-type task uses the time-of-day component
        // for its scheduled time; other types just carry midnight and are rendered/edited as a plain
        // date in the UI. No separate "MeetingTime" field needed since this column already supports it.
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Project Management module (Scrum/Kanban) - the same row serves the classic Tasks tab and the
        // PM backlog/boards, so no second work-item table. Type additionally allows Epic/Feature/Story/
        // Bug/Subtask; ParentId models that hierarchy (level rules enforced in PmWorkItemService).
        public int? ParentId { get; set; }
        public virtual ProjectTask? Parent { get; set; }
        public virtual ICollection<ProjectTask> Children { get; set; } = new List<ProjectTask>();
        public int? StoryPoints { get; set; }
        // First time the item entered "in-progress" (see WorkItemStatusTracker) - cycle time is
        // CompletedAt - StartedAt. Null for work that predates the PM module or never started.
        public DateTime? StartedAt { get; set; }
        // Backlog ordering within a project - lower sorts first. A double so a drag-reorder can take
        // the midpoint of its neighbours without renumbering every row.
        public double Rank { get; set; }

        public bool IsOverdue => Status != "completed" && DueDate < DateTime.Now;

        // Navigation properties
        public virtual ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();
        public virtual ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
}
