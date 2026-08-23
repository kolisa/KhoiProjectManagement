namespace KhoiProjectManagement.Domain
{
    public class Permission : BaseEntity
    {
        public string Resource { get; set; } = string.Empty; // e.g. "projects", "vault"

        public string Action { get; set; } = string.Empty; // e.g. "read", "write", "manage_roles"

        public string Name { get; set; } = string.Empty; // "resource.action" - used in claims/policies

        public string? Description { get; set; }

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
