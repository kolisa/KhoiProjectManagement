using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmRetroService
    {
        Task<List<RetroItemDto>> GetAsync(PmCaller caller, int sprintId);
        Task<RetroItemDto> AddAsync(PmCaller caller, int sprintId, SaveRetroItemDto dto);
        Task<RetroItemDto> UpdateAsync(PmCaller caller, int itemId, SaveRetroItemDto dto);
        Task<RetroItemDto> SetDoneAsync(PmCaller caller, int itemId, bool done);
        Task DeleteAsync(PmCaller caller, int itemId);
    }

    // Sprint retrospectives: went-well / to-improve / action cards attached to a sprint. Opens once the
    // sprint has started (a Planned sprint has nothing to look back on). Anyone who can collaborate on the
    // project can add cards; authors edit their own; Scrum Masters/Managers/Admins (ManageSprint) can
    // remove any card.
    public class PmRetroService : IPmRetroService
    {
        private readonly IRepository<RetroItem> _items;
        private readonly IRepository<Sprint> _sprints;
        private readonly IRepository<User> _users;
        private readonly IProjectAccessService _access;
        private readonly IUnitOfWork _uow;

        public PmRetroService(IRepository<RetroItem> items, IRepository<Sprint> sprints, IRepository<User> users, IProjectAccessService access, IUnitOfWork uow)
        {
            _items = items; _sprints = sprints; _users = users; _access = access; _uow = uow;
        }

        private async Task<Sprint> SprintAsync(int sprintId) =>
            await _sprints.Query().AsNoTracking().FirstOrDefaultAsync(s => s.Id == sprintId) ?? throw new KeyNotFoundException();

        private async Task<bool> CanModerateAsync(PmCaller caller, int projectId)
        {
            var role = await _access.GetRoleAsync(caller, projectId);
            return role == ProjectAccessService.SuperAdminRole || ProjectRoles.Allows(role, PmAction.ManageSprint);
        }

        public async Task<List<RetroItemDto>> GetAsync(PmCaller caller, int sprintId)
        {
            var sprint = await SprintAsync(sprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.View);
            var moderator = await CanModerateAsync(caller, sprint.ProjectId);
            var readOnly = await _access.IsReadOnlyAsync(caller);

            var rows = await _items.Query().AsNoTracking().Where(i => i.SprintId == sprintId)
                .OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
                .Select(i => new { i.Id, i.SprintId, i.Category, i.Text, i.AuthoredBy, Name = i.Author.Name, i.IsDone, i.CreatedAt })
                .ToListAsync();

            return rows.Select(i => new RetroItemDto
            {
                Id = i.Id, SprintId = i.SprintId, Category = i.Category, Text = i.Text, AuthorId = i.AuthoredBy, AuthorName = i.Name,
                IsDone = i.IsDone, CreatedAt = i.CreatedAt,
                CanEdit = !readOnly && i.AuthoredBy == caller.UserId, CanDelete = !readOnly && (i.AuthoredBy == caller.UserId || moderator),
            }).ToList();
        }

        public async Task<RetroItemDto> AddAsync(PmCaller caller, int sprintId, SaveRetroItemDto dto)
        {
            var sprint = await SprintAsync(sprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.Collaborate);
            if (sprint.State == SprintStates.Planned)
                throw new InvalidOperationException("A retrospective opens once the sprint has started.");

            var item = new RetroItem { SprintId = sprintId, Category = dto.Category, Text = dto.Text.Trim(), AuthoredBy = caller.UserId };
            _items.Add(item);
            await _uow.SaveChangesAsync();
            return await MapAsync(caller, item, sprint.ProjectId);
        }

        public async Task<RetroItemDto> UpdateAsync(PmCaller caller, int itemId, SaveRetroItemDto dto)
        {
            var item = await _items.Query().FirstOrDefaultAsync(i => i.Id == itemId) ?? throw new KeyNotFoundException();
            var sprint = await SprintAsync(item.SprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.Collaborate);
            if (item.AuthoredBy != caller.UserId) throw new ForbiddenException("You can only edit your own retrospective cards.");

            item.Text = dto.Text.Trim();
            item.Category = dto.Category;
            await _uow.SaveChangesAsync();
            return await MapAsync(caller, item, sprint.ProjectId);
        }

        // Ticking an action off is a team activity, so any collaborator may do it, not just the author.
        public async Task<RetroItemDto> SetDoneAsync(PmCaller caller, int itemId, bool done)
        {
            var item = await _items.Query().FirstOrDefaultAsync(i => i.Id == itemId) ?? throw new KeyNotFoundException();
            var sprint = await SprintAsync(item.SprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.Collaborate);
            if (item.Category != RetroCategories.Action) throw new InvalidOperationException("Only action items can be marked done.");

            item.IsDone = done;
            await _uow.SaveChangesAsync();
            return await MapAsync(caller, item, sprint.ProjectId);
        }

        public async Task DeleteAsync(PmCaller caller, int itemId)
        {
            var item = await _items.Query().FirstOrDefaultAsync(i => i.Id == itemId) ?? throw new KeyNotFoundException();
            var sprint = await SprintAsync(item.SprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.View);
            if (item.AuthoredBy != caller.UserId)
                await _access.RequireAsync(caller, sprint.ProjectId, PmAction.ManageSprint);

            _items.Remove(item);
            await _uow.SaveChangesAsync();
        }

        private async Task<RetroItemDto> MapAsync(PmCaller caller, RetroItem item, int projectId) => new()
        {
            Id = item.Id, SprintId = item.SprintId, Category = item.Category, Text = item.Text, AuthorId = item.AuthoredBy,
            AuthorName = await _users.Query().AsNoTracking().Where(u => u.Id == item.AuthoredBy).Select(u => u.Name).FirstOrDefaultAsync() ?? "Someone",
            IsDone = item.IsDone, CreatedAt = item.CreatedAt,
            CanEdit = item.AuthoredBy == caller.UserId,
            CanDelete = item.AuthoredBy == caller.UserId || await CanModerateAsync(caller, projectId),
        };
    }
}
