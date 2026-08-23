namespace KhoiProjectManagement.Application
{
    public class TimesheetDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        public string? ApproverName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? RejectionReason { get; set; }
        public List<TimesheetEntryDto> Entries { get; set; } = new();
        public decimal TotalHours => Entries.Sum(e => e.Hours);
    }

    public class TimesheetEntryDto
    {
        public int Id { get; set; }
        public DateTime EntryDate { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? Description { get; set; }
        public decimal Hours { get; set; }
    }

    public class CreateTimesheetDto
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<CreateTimesheetEntryDto> Entries { get; set; } = new();
    }

    public class CreateTimesheetEntryDto
    {
        public DateTime EntryDate { get; set; }
        public int? ProjectId { get; set; }
        public string? Description { get; set; }
        public decimal Hours { get; set; }
    }

    public class UpdateTimesheetDto
    {
        public List<CreateTimesheetEntryDto> Entries { get; set; } = new();
    }

    public class RejectTimesheetDto
    {
        public string Reason { get; set; } = string.Empty;
    }
}
