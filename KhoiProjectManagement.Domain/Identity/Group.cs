namespace KhoiProjectManagement.Domain
{
    // An ad-hoc, admin-managed collection of users, grantable as a SpacePermission target
    // alongside User and Role (see SpacePermission.cs) - unlike Role, a Group carries no flat
    // CRUD permissions of its own, it only exists to be assigned Space access as a unit.
    public class Group : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<UserGroup> UserGroups { get; set; } = new List<UserGroup>();
    }
}
