namespace KhoiProjectManagement.Domain
{
    public class Role : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        // Seeded roles (Admin/Manager/Member) cannot be deleted through the API.
        public bool IsSystemRole { get; set; }

        // True only for the seeded Admin role - never exposed on any DTO or settable through the API.
        // Grants an unconditional bypass of every permission check (see PermissionAuthorizationHandler
        // and SpacePermissionResolver) regardless of RolePermission/SpacePermission grants, so admin
        // access can't be silently misconfigured away by editing those.
        public bool IsSuperAdmin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
