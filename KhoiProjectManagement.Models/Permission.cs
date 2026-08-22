using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    public class Permission
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Resource { get; set; } = string.Empty; // e.g. "projects", "vault"

        [Required]
        [StringLength(50)]
        public string Action { get; set; } = string.Empty; // e.g. "read", "write", "manage_roles"

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty; // "resource.action" - used in claims/policies

        [StringLength(500)]
        public string? Description { get; set; }

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
