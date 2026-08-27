namespace KhoiProjectManagement.Application
{
    public interface IGroupService
    {
        Task<List<GroupDto>> GetGroupsAsync();
        Task<GroupDto> CreateGroupAsync(CreateGroupDto dto);
        Task<bool> UpdateGroupAsync(int id, UpdateGroupDto dto);

        // Returns null if the group doesn't exist.
        Task<List<int>?> GetGroupMembersAsync(int groupId);

        // Full-replace, mirrors RoleService.SetRolePermissionsAsync's join-table swap.
        Task<bool> SetGroupMembersAsync(int groupId, List<int> userIds);
    }
}
