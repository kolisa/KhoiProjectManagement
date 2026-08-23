namespace KhoiProjectManagement.Domain
{
    // Flat, record-oriented, like Invoice/OnboardingChecklist - a timesheet belongs to exactly one
    // user, no Space needed. Status is a string, matching Project.Status/Invoice.Status convention.
    public class Timesheet : BaseEntity
    {
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        public string Status { get; set; } = "Draft";

        public DateTime? SubmittedAt { get; set; }
        public int? ApprovedBy { get; set; }
        public virtual User? Approver { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }

        public virtual ICollection<TimesheetEntry> Entries { get; set; } = new List<TimesheetEntry>();
    }
}
