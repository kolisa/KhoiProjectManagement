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

    // Progress of a container (Epic / Feature / Story with subtasks) measured over the work underneath it:
    // only leaf descendants count, so a Feature is "3 of 8" in terms of the real tasks, not of its stories.
    public static class WorkItemRollup
    {
        public readonly record struct Node(int Id, int? ParentId, string Status, int? Points);
        public readonly record struct Result(int Done, int Total, int PointsDone, int PointsTotal);

        // One entry for every item that has at least one child.
        public static Dictionary<int, Result> Compute(IReadOnlyCollection<Node> nodes)
        {
            var childrenOf = nodes.Where(n => n.ParentId != null).ToLookup(n => n.ParentId!.Value);
            var memo = new Dictionary<int, Result>();

            Result Leaves(Node node)
            {
                if (memo.TryGetValue(node.Id, out var cached)) return cached;
                var kids = childrenOf[node.Id].ToList();
                Result result;
                if (kids.Count == 0)
                {
                    var done = node.Status == "completed";
                    var points = node.Points ?? 0;
                    result = new Result(done ? 1 : 0, 1, done ? points : 0, points);
                }
                else
                {
                    var sum = new Result(0, 0, 0, 0);
                    foreach (var kid in kids)
                    {
                        var r = Leaves(kid);
                        sum = new Result(sum.Done + r.Done, sum.Total + r.Total, sum.PointsDone + r.PointsDone, sum.PointsTotal + r.PointsTotal);
                    }
                    result = sum;
                }
                memo[node.Id] = result;
                return result;
            }

            var output = new Dictionary<int, Result>();
            foreach (var node in nodes.Where(n => childrenOf.Contains(n.Id)))
                output[node.Id] = Leaves(node);
            return output;
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
