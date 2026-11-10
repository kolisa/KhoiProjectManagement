using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Domain
{
    public class ProjectUser
    {
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        // Project Management module role: ProjectAdmin, ProjectManager, ScrumMaster, Developer, Tester,
        // Viewer. Existing memberships default to Developer; it has no effect outside the PM module.
        public string ProjectRole { get; set; } = "Developer";
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
