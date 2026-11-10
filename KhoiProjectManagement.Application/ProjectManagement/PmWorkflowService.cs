using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmWorkflowService
    {
        Task<WorkflowDto> GetAsync(PmCaller caller, int projectId);
        Task<WorkflowDto> SaveAsync(PmCaller caller, int projectId, SaveWorkflowDto dto);
        // Throws InvalidOperationException when the project's workflow forbids from -> to. No authorization
        // here: callers have already authorized the status change itself.
        Task EnsureTransitionAllowedAsync(int projectId, string from, string to);
    }

    // Per-project workflow rules. The statuses themselves stay the fixed four (the classic Tasks tab,
    // notifications, reports and status history all rely on them); what a project configures is which
    // moves between them are permitted. Enforced on PM board moves and PM work-item edits.
    public class PmWorkflowService : IPmWorkflowService
    {
        public static readonly string[] Statuses = { "todo", "in-progress", "blocked", "completed" };
        private const string Done = "completed";

        private readonly IRepository<WorkflowRestriction> _restrictions;
        private readonly IProjectAccessService _access;
        private readonly IUnitOfWork _uow;

        public PmWorkflowService(IRepository<WorkflowRestriction> restrictions, IProjectAccessService access, IUnitOfWork uow)
        {
            _restrictions = restrictions;
            _access = access;
            _uow = uow;
        }

        public async Task<WorkflowDto> GetAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            var blocked = await LoadBlockedAsync(projectId);
            return Build(blocked);
        }

        public async Task<WorkflowDto> SaveAsync(PmCaller caller, int projectId, SaveWorkflowDto dto)
        {
            await _access.RequireAsync(caller, projectId, PmAction.ManageBoard);

            var blocked = new HashSet<(string, string)>();
            foreach (var t in dto.Transitions)
            {
                if (!Statuses.Contains(t.From) || !Statuses.Contains(t.To))
                    throw new InvalidOperationException("Unknown status in the workflow.");
                if (t.From == t.To) continue;
                if (!t.Allowed) blocked.Add((t.From, t.To));
            }

            // Every status must still be able to reach Done, otherwise items could be stranded.
            foreach (var status in Statuses.Where(s => s != Done))
                if (!CanReach(status, Done, blocked))
                    throw new InvalidOperationException($"Work in \"{Label(status)}\" must still be able to reach \"{Label(Done)}\" (directly or through other statuses).");

            var existing = await _restrictions.Query().Where(r => r.ProjectId == projectId).ToListAsync();
            _restrictions.RemoveRange(existing);
            foreach (var (from, to) in blocked)
                _restrictions.Add(new WorkflowRestriction { ProjectId = projectId, FromStatus = from, ToStatus = to });
            await _uow.SaveChangesAsync();

            return Build(blocked);
        }

        public async Task EnsureTransitionAllowedAsync(int projectId, string from, string to)
        {
            if (from == to) return;
            if (await _restrictions.Query().AnyAsync(r => r.ProjectId == projectId && r.FromStatus == from && r.ToStatus == to))
                throw new InvalidOperationException($"This project's workflow does not allow moving from \"{Label(from)}\" to \"{Label(to)}\".");
        }

        private async Task<HashSet<(string, string)>> LoadBlockedAsync(int projectId) =>
            (await _restrictions.Query().AsNoTracking().Where(r => r.ProjectId == projectId)
                .Select(r => new { r.FromStatus, r.ToStatus }).ToListAsync())
            .Select(r => (r.FromStatus, r.ToStatus)).ToHashSet();

        private static WorkflowDto Build(HashSet<(string, string)> blocked) => new()
        {
            Statuses = Statuses.ToList(),
            IsCustomised = blocked.Count > 0,
            Transitions = Statuses.SelectMany(f => Statuses.Where(t => t != f).Select(t => new WorkflowTransitionDto
            {
                From = f, To = t, Allowed = !blocked.Contains((f, t)),
            })).ToList(),
        };

        private static bool CanReach(string from, string goal, HashSet<(string, string)> blocked)
        {
            var seen = new HashSet<string> { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == goal) return true;
                foreach (var next in Statuses.Where(s => s != current && !blocked.Contains((current, s)) && seen.Add(s)))
                    queue.Enqueue(next);
            }
            return false;
        }

        private static string Label(string status) => status switch
        {
            "todo" => "To do", "in-progress" => "In progress", "blocked" => "Blocked", "completed" => "Done", _ => status,
        };
    }
}
