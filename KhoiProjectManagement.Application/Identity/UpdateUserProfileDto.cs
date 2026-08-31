namespace KhoiProjectManagement.Application
{
    // Deliberately has no Role field - role/permission assignment is a separate, more tightly-guarded
    // endpoint (PUT /api/users/{id}/roles, gated by users.manage_roles) so a caller authorized only to
    // edit profiles can't also escalate a user's role. See AssignUserRolesDto.
    public class UpdateUserProfileDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string? Password { get; set; }
        public int? ManagerId { get; set; }

        // Optional, and nullable-means-"leave unchanged" (same convention as Password above) - lets a
        // birthday be added/corrected later without ever needing to read the existing value back.
        public DateTime? DateOfBirth { get; set; }
    }
}
