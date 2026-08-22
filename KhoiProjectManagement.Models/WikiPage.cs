using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // A wiki page lives inside a Space and is authorized exactly like VaultEntry - proving the
    // Space/SpacePermission model genuinely generalizes, not just fits the vault. ParentPageId nests
    // pages within one Space for navigation only; it is not a permission boundary - Space.ParentSpaceId
    // remains the only thing that scopes access.
    public class WikiPage : ISpaceScoped
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public int SpaceId { get; set; }
        public virtual Space Space { get; set; } = null!;

        public int? ParentPageId { get; set; }
        public virtual WikiPage? ParentPage { get; set; }
        public virtual ICollection<WikiPage> ChildPages { get; set; } = new List<WikiPage>();

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;

        public int? UpdatedBy { get; set; }
        public virtual User? Updater { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<WikiPageVersion> Versions { get; set; } = new List<WikiPageVersion>();
        public virtual ICollection<WikiPageComment> Comments { get; set; } = new List<WikiPageComment>();
    }
}
