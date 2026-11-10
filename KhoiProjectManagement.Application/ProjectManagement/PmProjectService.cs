using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmProjectService
    {
        Task<List<PmProjectSummaryDto>> GetMyProjectsAsync(PmCaller caller);
        Task<List<PmMemberDto>> GetMembersAsync(PmCaller caller, int projectId);
        Task SetMemberAsync(PmCaller caller, int projectId, int userId, string role);
        Task RemoveMemberAsync(PmCaller caller, int projectId, int userId);
        Task<ImportMembersResultDto> ImportMembersAsync(PmCaller caller, int projectId, ImportMembersDto dto);
        Task<List<PmUserLookupDto>> GetAssignableUsersAsync(PmCaller caller);

        // SuperAdmin module-access administration (controller gates on the pm.admin permission).
        Task<List<PmAccessUserDto>> GetAccessUsersAsync();
        Task SetAccessAsync(int actingUserId, int userId, SetPmAccessDto dto);
    }

    public class PmProjectService : IPmProjectService
    {
        private readonly IRepository<Project> _projects;
        private readonly IRepository<ProjectUser> _members;
        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<User> _users;
        private readonly IRepository<UserRole> _userRoles;
        private readonly IProjectAccessService _access;
        private readonly IActivityLogService _activity;
        private readonly IUnitOfWork _unitOfWork;

        public PmProjectService(
            IRepository<Project> projects,
            IRepository<ProjectUser> members,
            IRepository<ProjectTask> tasks,
            IRepository<User> users,
            IRepository<UserRole> userRoles,
            IProjectAccessService access,
            IActivityLogService activity,
            IUnitOfWork unitOfWork)
        {
            _projects = projects;
            _members = members;
            _tasks = tasks;
            _users = users;
            _userRoles = userRoles;
            _access = access;
            _activity = activity;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<PmProjectSummaryDto>> GetMyProjectsAsync(PmCaller caller)
        {
            if (!await _access.HasModuleAccessAsync(caller))
                throw new ForbiddenException("You do not have access to the Project Management module.");

            var readOnly = await _access.IsReadOnlyAsync(caller);
            var projects = _projects.Query().AsNoTracking();
            if (!caller.IsSuperAdmin)
                projects = projects.Where(p => p.ProjectUsers.Any(m => m.UserId == caller.UserId));

            var rows = await projects.OrderBy(p => p.Name).Select(p => new
            {
                p.Id,
                p.Name,
                p.Status,
                Role = p.ProjectUsers.Where(m => m.UserId == caller.UserId).Select(m => m.ProjectRole).FirstOrDefault(),
                Total = p.Tasks.Count(),
                Open = p.Tasks.Count(t => t.Status != "completed"),
            }).ToListAsync();

            return rows.Select(r => new PmProjectSummaryDto
            {
                Id = r.Id,
                Name = r.Name,
                Status = r.Status,
                MyRole = caller.IsSuperAdmin ? ProjectAccessService.SuperAdminRole : r.Role ?? string.Empty,
                ReadOnly = readOnly,
                TotalItems = r.Total,
                OpenItems = r.Open,
            }).ToList();
        }

        public async Task<List<PmMemberDto>> GetMembersAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            return await _members.Query().AsNoTracking()
                .Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.User.Name)
                .Select(m => new PmMemberDto { UserId = m.UserId, Name = m.User.Name, Email = m.User.Email, Role = m.ProjectRole, ModuleReadOnly = m.User.PmReadOnly })
                .ToListAsync();
        }

        public async Task SetMemberAsync(PmCaller caller, int projectId, int userId, string role)
        {
            await _access.RequireAsync(caller, projectId, PmAction.ManageMembers);

            var target = await _users.Query().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive)
                ?? throw new InvalidOperationException("User not found or inactive.");
            if (!target.PmAccess)
                throw new InvalidOperationException("This user has not been granted Project Management module access. Ask a SuperAdmin to enable it first.");

            var existing = await _members.Query().FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);
            if (existing == null)
                _members.Add(new ProjectUser { ProjectId = projectId, UserId = userId, ProjectRole = role });
            else
            {
                if (existing.ProjectRole == ProjectRoles.ProjectAdmin && role != ProjectRoles.ProjectAdmin)
                    await EnsureAnotherAdminAsync(projectId, userId);
                existing.ProjectRole = role;
            }

            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Project", projectId, $"{target.Name} -> {role}", caller.UserId, "member-role-set");
        }

        public const int MaxImportRows = 500;

        public sealed record PermissionCountRow(int UserId, int Count);

        // Static query builders: their SQL translation is covered by tests that need no database.
        public static IQueryable<User> UsersByEmailQuery(IQueryable<User> users, IReadOnlyCollection<string> lowerCaseEmails) =>
            users.Where(u => lowerCaseEmails.Contains(u.Email.ToLower()));

        public static IQueryable<PermissionCountRow> PermissionCountsQuery(IQueryable<UserRole> userRoles, IReadOnlyCollection<int> userIds) =>
            userRoles.AsNoTracking().Where(ur => userIds.Contains(ur.UserId))
                .Select(ur => new PermissionCountRow(ur.UserId, ur.Role.RolePermissions.Count()));

        // Bulk-adds (or re-roles) people on a project from a pasted/uploaded list of "email, role" rows. Matching is
        // by email against EXISTING accounts - this never creates accounts. Rows that cannot be applied are
        // reported with the reason and skipped; the rest are applied together, and a DryRun applies nothing.
        public async Task<ImportMembersResultDto> ImportMembersAsync(PmCaller caller, int projectId, ImportMembersDto dto)
        {
            await _access.RequireAsync(caller, projectId, PmAction.ManageMembers);
            if (dto.Rows.Count == 0) throw new InvalidOperationException("There are no rows to import.");
            if (dto.Rows.Count > MaxImportRows) throw new InvalidOperationException($"An import can have at most {MaxImportRows} rows.");
            if (dto.GrantModuleAccess && !caller.IsPmAdmin && !caller.IsSuperAdmin)
                throw new ForbiddenException("Only a SuperAdmin can grant Project Management module access.");
            if (dto.ModuleOnly && !dto.GrantModuleAccess)
                throw new InvalidOperationException("Module-only accounts must also be granted module access.");
            if (dto.ReadOnlyAccess && !dto.GrantModuleAccess)
                throw new InvalidOperationException("A read-only access level only applies when module access is being granted.");

            var wantedEmails = dto.Rows.Select(r => (r.Email ?? string.Empty).Trim().ToLowerInvariant()).Where(e => e.Length > 0).Distinct().ToList();
            var users = await UsersByEmailQuery(_users.Query(), wantedEmails).ToListAsync();
            var userByEmail = users.GroupBy(u => u.Email.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
            var roster = await _members.Query().Where(m => m.ProjectId == projectId).ToListAsync();
            var memberByUser = roster.ToDictionary(m => m.UserId);
            var hadAdmin = roster.Any(m => m.ProjectRole == ProjectRoles.ProjectAdmin);
            var finalRoles = roster.ToDictionary(m => m.UserId, m => m.ProjectRole);

            // Only people who are allowed to see the directory get to learn WHY an email can't be added; a
            // project admin otherwise could probe which addresses have accounts, and which are deactivated.
            var seesDirectory = caller.IsPmAdmin || caller.IsSuperAdmin;

            // "Confine to this module" locks an account out of everything else in the app, so it is only applied to
            // accounts that have no other permissions - never to someone who also uses finance, HR, the wiki...
            var otherPermissions = new Dictionary<int, int>();
            if (dto.GrantModuleAccess && dto.ModuleOnly && users.Count > 0)
            {
                var ids = users.Select(u => u.Id).ToList();
                var counts = await PermissionCountsQuery(_userRoles.Query(), ids).ToListAsync();
                otherPermissions = counts.GroupBy(c => c.UserId).ToDictionary(g => g.Key, g => g.Sum(c => c.Count));
            }

            var result = new ImportMembersResultDto { DryRun = dto.DryRun };
            var claimed = new HashSet<string>();
            var rowNumber = 0;

            foreach (var raw in dto.Rows)
            {
                rowNumber++;
                var email = (raw.Email ?? string.Empty).Trim();
                var row = new ImportMemberResultRowDto { Row = rowNumber, Email = email };
                result.Rows.Add(row);

                void Reject(string outcome, string message) { row.Outcome = outcome; row.Message = message; }
                void Unavailable(string detailedOutcome, string detailedMessage)
                {
                    if (seesDirectory) Reject(detailedOutcome, detailedMessage);
                    else Reject(ImportOutcomes.Unavailable, "This person can't be added: there is no active account with Project Management access for this email.");
                }

                if (email.Length == 0 || email.Length > 254 || !email.Contains('@') || email.Any(char.IsWhiteSpace))
                { Reject(ImportOutcomes.InvalidEmail, "That is not a valid email address."); continue; }

                var key = email.ToLowerInvariant();
                // An email is only "claimed" by a row that is itself valid, so a mistake on an earlier line never
                // causes a later, correct line for the same person to be rejected as a duplicate.
                if (claimed.Contains(key))
                { Reject(ImportOutcomes.Duplicate, "This email appears earlier in the list; the first valid row wins."); continue; }

                string role;
                if (string.IsNullOrWhiteSpace(raw.Role)) role = ProjectRoles.Developer;
                else
                {
                    var parsed = ProjectRoles.Parse(raw.Role);
                    if (parsed == null)
                    { Reject(ImportOutcomes.InvalidRole, $"\"{raw.Role}\" is not a known role (use Project Admin, Project Manager, Scrum Master, Developer, Tester or Viewer)."); continue; }
                    role = parsed;
                }
                row.Role = role;

                if (!userByEmail.TryGetValue(key, out var user))
                { Unavailable(ImportOutcomes.NotFound, "No account with this email. Accounts are created by an administrator, not by this import."); continue; }
                if (!user.IsActive)
                { Unavailable(ImportOutcomes.Inactive, "This account is deactivated."); continue; }

                var grant = !user.PmAccess && dto.GrantModuleAccess;
                if (!user.PmAccess && !dto.GrantModuleAccess)
                { Unavailable(ImportOutcomes.NoModuleAccess, "This person has no Project Management module access yet. A SuperAdmin can enable it, or tick the grant option."); continue; }

                claimed.Add(key);
                memberByUser.TryGetValue(user.Id, out var existing);
                if (existing != null && existing.ProjectRole == role && !grant)
                { row.Outcome = ImportOutcomes.Unchanged; row.Message = "Already on the project with this role."; continue; }

                row.Applied = true;
                if (existing == null)
                {
                    row.Outcome = ImportOutcomes.Added; row.Message = "Added to the project."; result.Added++;
                }
                else if (existing.ProjectRole != role)
                {
                    row.Outcome = ImportOutcomes.Updated; row.Message = $"Role changed from {existing.ProjectRole} to {role}."; result.Updated++;
                }
                else
                {
                    row.Outcome = ImportOutcomes.AccessGranted; row.Message = "Role unchanged.";
                }

                var confine = false;
                if (grant)
                {
                    result.ModuleAccessGranted++;
                    confine = dto.ModuleOnly && otherPermissions.GetValueOrDefault(user.Id) == 0;
                    row.ConfinedToModule = confine;
                    row.Message += confine
                        ? " Module access granted; this account is confined to the module."
                        : dto.ModuleOnly
                            ? " Module access granted. Not confined to the module, because this account has other permissions in the app."
                            : " Module access granted.";
                    if (dto.ReadOnlyAccess) row.Message += " Read-only access.";
                }
                finalRoles[user.Id] = role;

                if (dto.DryRun) continue;
                if (grant) { user.PmAccess = true; user.PmOnly = confine; user.PmReadOnly = dto.ReadOnlyAccess; }
                if (existing == null) _members.Add(new ProjectUser { ProjectId = projectId, UserId = user.Id, ProjectRole = role });
                else existing.ProjectRole = role;
            }

            if (hadAdmin && !finalRoles.Values.Any(r => r == ProjectRoles.ProjectAdmin))
                throw new InvalidOperationException("This import would leave the project without a Project Admin. Keep at least one.");

            result.Skipped = result.Rows.Count(r => !r.Applied);
            if (!dto.DryRun && result.Rows.Any(r => r.Applied))
            {
                await _unitOfWork.SaveChangesAsync();
                await _activity.LogAsync("Project", projectId, "Members", caller.UserId, "members-imported",
                    $"{result.Added} added, {result.Updated} updated" + (result.ModuleAccessGranted > 0 ? $", {result.ModuleAccessGranted} granted module access" : string.Empty));
            }
            return result;
        }

        public async Task RemoveMemberAsync(PmCaller caller, int projectId, int userId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.ManageMembers);

            var existing = await _members.Query().Include(m => m.User)
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId)
                ?? throw new KeyNotFoundException();

            if (existing.ProjectRole == ProjectRoles.ProjectAdmin)
                await EnsureAnotherAdminAsync(projectId, userId);

            // Their open items shouldn't point at someone who is no longer on the project.
            var assigned = await _tasks.Query().Where(t => t.ProjectId == projectId && t.AssignedToId == userId).ToListAsync();
            foreach (var task in assigned) task.AssignedToId = null;

            _members.Remove(existing);
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Project", projectId, existing.User.Name, caller.UserId, "member-removed");
        }

        public async Task<List<PmUserLookupDto>> GetAssignableUsersAsync(PmCaller caller)
        {
            if (!await _access.HasModuleAccessAsync(caller))
                throw new ForbiddenException("You do not have access to the Project Management module.");
            return await _users.Query().AsNoTracking()
                .Where(u => u.IsActive && u.PmAccess)
                .OrderBy(u => u.Name)
                .Select(u => new PmUserLookupDto { Id = u.Id, Name = u.Name, Email = u.Email })
                .ToListAsync();
        }

        public async Task<List<PmAccessUserDto>> GetAccessUsersAsync() =>
            await _users.Query().AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .Select(u => new PmAccessUserDto { Id = u.Id, Name = u.Name, Email = u.Email, PmAccess = u.PmAccess, PmOnly = u.PmOnly, PmReadOnly = u.PmReadOnly })
                .ToListAsync();

        public async Task SetAccessAsync(int actingUserId, int userId, SetPmAccessDto dto)
        {
            var user = await _users.Query().FirstOrDefaultAsync(u => u.Id == userId) ?? throw new KeyNotFoundException();

            // Module-only confinement is meaningless (and would lock the account out of everything)
            // without module access itself.
            if (dto.PmOnly && !dto.PmAccess)
                throw new InvalidOperationException("A module-only user must also have Project Management access.");

            // Likewise a read-only level only means something for someone who has access.
            if (dto.ReadOnly && !dto.PmAccess)
                throw new InvalidOperationException("Read-only access needs Project Management access.");

            user.PmAccess = dto.PmAccess;
            user.PmOnly = dto.PmOnly;
            user.PmReadOnly = dto.PmAccess && dto.ReadOnly;
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("User", user.Id, user.Name, actingUserId, "pm-access-set",
                $"access={(dto.PmAccess ? (dto.ReadOnly ? "read-only" : "read-write") : "none")}, moduleOnly={dto.PmOnly}");
        }

        private async Task EnsureAnotherAdminAsync(int projectId, int excludingUserId)
        {
            var other = await _members.Query().AnyAsync(m =>
                m.ProjectId == projectId && m.UserId != excludingUserId && m.ProjectRole == ProjectRoles.ProjectAdmin);
            if (!other)
                throw new InvalidOperationException("A project must keep at least one Project Admin.");
        }
    }
}
