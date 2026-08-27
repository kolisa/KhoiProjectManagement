namespace KhoiProjectManagement.Application
{
    public class GroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int MemberCount { get; set; }
    }

    public class CreateGroupDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateGroupDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class SetGroupMembersDto
    {
        public List<int> UserIds { get; set; } = new();
    }
}
