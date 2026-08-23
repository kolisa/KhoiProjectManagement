using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Application
{
    public class ProjectSummaryReportDto
    {
        public string Title { get; set; } = "Project Summary Report";
        public DateTime GeneratedAt { get; set; }
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public double OverallCompletionRate { get; set; }
        public List<ProjectSummaryItemDto> Projects { get; set; } = new();
    }
}
