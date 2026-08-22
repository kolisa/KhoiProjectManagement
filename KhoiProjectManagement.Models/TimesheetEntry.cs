using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    public class TimesheetEntry
    {
        public int Id { get; set; }

        public int TimesheetId { get; set; }
        public virtual Timesheet Timesheet { get; set; } = null!;

        public DateTime EntryDate { get; set; }

        // Nullable - hours aren't always billable to a project (PTO, admin time, etc.).
        public int? ProjectId { get; set; }
        public virtual Project? Project { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public decimal Hours { get; set; }
    }
}
