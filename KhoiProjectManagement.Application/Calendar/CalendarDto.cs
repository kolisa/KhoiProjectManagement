namespace KhoiProjectManagement.Application
{
    public class CalendarFeedDto
    {
        public List<BirthdayEntryDto> Birthdays { get; set; } = new();
        public List<CompanyEventDto> Events { get; set; } = new();
    }

    // Only month/day are ever exposed here - never the birth year, which has no calendar purpose and
    // is the sensitive part of DateOfBirth (see plan Phase 12.1).
    public class BirthdayEntryDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Month { get; set; }
        public int Day { get; set; }
    }

    public class CompanyEventDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int? SubjectUserId { get; set; }
        public string? SubjectName { get; set; }
        public string CreatorName { get; set; } = string.Empty;
    }

    public class CreateCompanyEventDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
        public string EventType { get; set; } = "Event";
        public int? SubjectUserId { get; set; }
    }

    public class SetDateOfBirthDto
    {
        public DateTime DateOfBirth { get; set; }
    }
}
