using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhoiProjectManagement.Application
{
    public class DashboardStatisticsDto
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int TodoTasks { get; set; }
        public int BlockedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double CompletionRate { get; set; }

        // Null when no DashboardStatsSnapshot >= 7 days old exists yet (e.g. the first week after this
        // shipped) - the frontend renders no delta badge rather than fabricate a trend.
        public int? ActiveProjectsDelta { get; set; }
        public int? TotalTasksDelta { get; set; }
        public int? OverdueTasksDelta { get; set; }
        public double? CompletionRateDelta { get; set; }
    }
}
