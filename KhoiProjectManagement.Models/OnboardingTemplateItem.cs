using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    public class OnboardingTemplateItem
    {
        public int Id { get; set; }

        public int TemplateId { get; set; }
        public virtual OnboardingTemplate Template { get; set; } = null!;

        [Required]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;

        public int SortOrder { get; set; }
    }
}
