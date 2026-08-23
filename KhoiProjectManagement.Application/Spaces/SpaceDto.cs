namespace KhoiProjectManagement.Application
{
    public class SpaceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentSpaceId { get; set; }
        public string SpaceType { get; set; } = string.Empty;
        public bool InheritPermissions { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }

        // The caller's own effective PermissionLevel on this Space ("Read"/"Write"/"Manage"), so a
        // frontend can show/hide affordances without trial-and-error against 403s.
        public string? MyEffectiveLevel { get; set; }
    }

    public class CreateSpaceDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? ParentSpaceId { get; set; }
        public string SpaceType { get; set; } = "Generic";
        public bool InheritPermissions { get; set; } = true;
    }

    public class UpdateSpaceDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool InheritPermissions { get; set; } = true;
    }
}
