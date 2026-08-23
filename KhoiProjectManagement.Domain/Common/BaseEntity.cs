namespace KhoiProjectManagement.Domain
{
    // Shared by every entity that has a single int primary key. The 6 pure join entities
    // (ProjectTag, ProjectUser, RolePermission, TaskTag, UserRole, WikiPageTag) deliberately do NOT
    // inherit this - they have composite keys (configured in ProjectManagementContext.OnModelCreating)
    // and no single Id, so a shared int Id base would misrepresent their identity. EF Core's PK
    // convention recognizes "Id" regardless of where in the hierarchy it's declared, and an abstract
    // base with no DbSet of its own does not turn the derived types into a TPH hierarchy - each
    // concrete entity keeps its own table exactly as before.
    public abstract class BaseEntity
    {
        public int Id { get; set; }
    }
}
