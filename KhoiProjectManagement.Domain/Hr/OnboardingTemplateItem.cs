namespace KhoiProjectManagement.Domain
{
    public class OnboardingTemplateItem : BaseEntity
    {
        public int TemplateId { get; set; }
        public virtual OnboardingTemplate Template { get; set; } = null!;

        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }
}
