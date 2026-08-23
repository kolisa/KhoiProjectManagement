using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Application
{
    public class ProjectDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public List<TeamMemberDto> TeamMembers { get; set; } = new();
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
    }
}
