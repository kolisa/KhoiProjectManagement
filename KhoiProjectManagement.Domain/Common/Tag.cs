using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Domain
{
    public class Tag : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public string Color { get; set; } = "#3B82F6";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<ProjectTag> ProjectTags { get; set; } = new List<ProjectTag>();
        public virtual ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();
        public virtual ICollection<WikiPageTag> WikiPageTags { get; set; } = new List<WikiPageTag>();
    }
}
