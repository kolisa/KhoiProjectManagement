using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Application
{
    public class TaskDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? AssignedToId { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsOverdue { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
