namespace KhoiProjectManagement.Domain
{
    // Flat, admin-managed checklist definition - not Space-scoped, since onboarding belongs to
    // exactly one User, not a browsable tree (see plan Phase 7).
    public class OnboardingTemplate : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public virtual ICollection<OnboardingTemplateItem> Items { get; set; } = new List<OnboardingTemplateItem>();
    }
}
