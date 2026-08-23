using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Domain
{
    public class Notification : BaseEntity
    {
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string Type { get; set; } = string.Empty; // overdue, assignment, completion, etc.

        public string Message { get; set; } = string.Empty;

        public int? TaskId { get; set; }
        public virtual ProjectTask? Task { get; set; }

        public int? ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        // Added for the "mention" type (see NotificationPreference) - mirrors TaskId/ProjectId's
        // sparse-per-feature FK pattern rather than introducing a generic polymorphic reference.
        public int? WikiPageId { get; set; }
        public virtual WikiPage? WikiPage { get; set; }

        public int? IdeaId { get; set; }
        public virtual Idea? Idea { get; set; }

        public int? ReminderId { get; set; }
        public virtual Reminder? Reminder { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
    }
}
