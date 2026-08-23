namespace KhoiProjectManagement.Application
{
    public class OnboardingTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<OnboardingTemplateItemDto> Items { get; set; } = new();
    }

    public class OnboardingTemplateItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class CreateOnboardingTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public List<string> ItemTitles { get; set; } = new();
    }

    public class UpdateOnboardingTemplateDto
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<string> ItemTitles { get; set; } = new();
    }

    public class OnboardingChecklistDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<OnboardingChecklistItemDto> Items { get; set; } = new();
    }

    public class OnboardingChecklistItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsComplete { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedByName { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateOnboardingChecklistDto
    {
        public int UserId { get; set; }
        public int TemplateId { get; set; }
    }

    public class UpdateChecklistItemDto
    {
        public bool IsComplete { get; set; }
        public string? Notes { get; set; }
    }
}
