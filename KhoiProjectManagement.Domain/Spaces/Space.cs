namespace KhoiProjectManagement.Domain
{
    // Informational only - never read by the authorization resolver, which treats every Space identically.
    public enum SpaceType
    {
        Generic,
        VaultRoot,
        VaultCategory,
        WikiSpace,
        ProjectSpace
    }

    // A generic hierarchical container (SharePoint site / Confluence space equivalent). Permission
    // grants (SpacePermission) attach to a Space and, unless InheritPermissions is broken, cascade to
    // its descendants. Items that live inside a Space (VaultEntry now, WikiPage later) implement
    // ISpaceScoped and are authorized against the Space they belong to, not against themselves.
    public class Space : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int? ParentSpaceId { get; set; }
        public virtual Space? ParentSpace { get; set; }
        public virtual ICollection<Space> ChildSpaces { get; set; } = new List<Space>();

        public SpaceType SpaceType { get; set; } = SpaceType.Generic;

        // SharePoint-style inheritance boundary: false means this Space is a hard permission
        // boundary - resolution stops here even if there's no local grant (net effect: deny).
        public bool InheritPermissions { get; set; } = true;

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        public virtual ICollection<SpacePermission> Permissions { get; set; } = new List<SpacePermission>();
    }
}
