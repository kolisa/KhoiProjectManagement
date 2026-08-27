namespace KhoiProjectManagement.Application
{
    public class SpacePermissionDto
    {
        public int Id { get; set; }
        public int? RoleId { get; set; }
        public string? RoleName { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
        public string Level { get; set; } = string.Empty;
    }

    // A grantee is exactly one of RoleId/UserId/GroupId - validated in SpaceService.
    public class SetSpacePermissionDto
    {
        public int? RoleId { get; set; }
        public int? UserId { get; set; }
        public int? GroupId { get; set; }
        public string Level { get; set; } = "Read";
    }
}
