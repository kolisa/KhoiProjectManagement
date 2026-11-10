using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    // Single place a status transition is recorded, so history (and therefore cycle time / cumulative
    // flow) stays complete whichever path changed the status: the classic Tasks tab, the backlog, or the board.
    public static class WorkItemStatusTracker
    {
        public static void Record(ProjectTask task, string? from, string to, int? userId, IRepository<TaskStatusHistory> history)
        {
            if (from == to) return;
            var now = DateTime.UtcNow;
            history.Add(new TaskStatusHistory { Task = task, FromStatus = from, ToStatus = to, ChangedAt = now, ChangedById = userId });
            if (to == "in-progress" && task.StartedAt == null) task.StartedAt = now;
        }
    }

    public static class RankCalculator
    {
        public const double Step = 1000;

        // `others` is every item the moved one is being ranked against (it must already exclude the moved
        // item). afterItemId == null -> top of that set; otherwise directly after the given item.
        public static async Task<double> ComputeAsync(IQueryable<ProjectTask> others, int? afterItemId, string notFoundMessage)
        {
            if (afterItemId == null)
            {
                var first = await others.OrderBy(t => t.Rank).Select(t => (double?)t.Rank).FirstOrDefaultAsync();
                return first.HasValue ? first.Value - Step : Step;
            }

            var after = await others.Where(t => t.Id == afterItemId).Select(t => (double?)t.Rank).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException(notFoundMessage);
            var next = await others.Where(t => t.Rank > after).OrderBy(t => t.Rank).Select(t => (double?)t.Rank).FirstOrDefaultAsync();
            return next.HasValue ? (after + next.Value) / 2 : after + Step;
        }
    }
}
