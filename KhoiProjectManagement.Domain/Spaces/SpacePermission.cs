namespace KhoiProjectManagement.Domain
{
    // Ordinal matters: Manage implies Write implies Read (SpacePermissionResolver takes the max).
    public enum PermissionLevel
    {
        Read = 1,
        Write = 2,
        Manage = 3
    }

    // Grants a Role or a specific User a PermissionLevel on a Space. Exactly one of RoleId/UserId is
    // set - RoleId for role-wide grants (e.g. seeded Admin gets Manage on the Vault root), UserId for
    // per-person grants (e.g. project team membership synced from ProjectUser, see ProjectService).
    public class SpacePermission : BaseEntity
    {
        public int SpaceId { get; set; }
        public virtual Space Space { get; set; } = null!;

        public int? RoleId { get; set; }
        public virtual Role? Role { get; set; }

        public int? UserId { get; set; }
        public virtual User? User { get; set; }

        public PermissionLevel Level { get; set; }

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
