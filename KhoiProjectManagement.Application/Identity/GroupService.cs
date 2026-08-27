using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class GroupService : IGroupService
    {
        private readonly IRepository<Group> _groupRepo;
        private readonly IRepository<UserGroup> _userGroupRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IUnitOfWork _unitOfWork;

        public GroupService(
            IRepository<Group> groupRepo,
            IRepository<UserGroup> userGroupRepo,
            IRepository<User> userRepo,
            IUnitOfWork unitOfWork)
        {
            _groupRepo = groupRepo;
            _userGroupRepo = userGroupRepo;
            _userRepo = userRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<GroupDto>> GetGroupsAsync()
        {
            var groups = await _groupRepo.Query().ToListAsync();
            var memberCounts = await _userGroupRepo.Query()
                .GroupBy(ug => ug.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            return groups.Select(g => MapGroup(g, memberCounts.GetValueOrDefault(g.Id))).ToList();
        }

        public async Task<GroupDto> CreateGroupAsync(CreateGroupDto dto)
        {
            var group = new Group
            {
                Name = dto.Name,
                Description = dto.Description
            };

            _groupRepo.Add(group);
            await _unitOfWork.SaveChangesAsync();

            return MapGroup(group, 0);
        }

        public async Task<bool> UpdateGroupAsync(int id, UpdateGroupDto dto)
        {
            var group = await _groupRepo.Query().FirstOrDefaultAsync(g => g.Id == id);
            if (group == null)
                return false;

            group.Name = dto.Name;
            group.Description = dto.Description;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<int>?> GetGroupMembersAsync(int groupId)
        {
            var groupExists = await _groupRepo.Query().AnyAsync(g => g.Id == groupId);
            if (!groupExists)
                return null;

            return await _userGroupRepo.Query()
                .Where(ug => ug.GroupId == groupId)
                .Select(ug => ug.UserId)
                .ToListAsync();
        }

        public async Task<bool> SetGroupMembersAsync(int groupId, List<int> userIds)
        {
            var group = await _groupRepo.Query().FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
                return false;

            var distinctUserIds = userIds.Distinct().ToList();
            var existingUserCount = await _userRepo.Query().CountAsync(u => distinctUserIds.Contains(u.Id));
            if (existingUserCount != distinctUserIds.Count)
                return false;

            var existing = _userGroupRepo.Query().Where(ug => ug.GroupId == groupId);
            _userGroupRepo.RemoveRange(existing);

            _userGroupRepo.AddRange(distinctUserIds.Select(userId => new UserGroup { GroupId = groupId, UserId = userId }));

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        private static GroupDto MapGroup(Group group, int memberCount) => new()
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            MemberCount = memberCount
        };
    }
}
