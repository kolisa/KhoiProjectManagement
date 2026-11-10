using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // The service tests mock IRepository<T> over in-memory lists, so they run LINQ-to-objects - which happily
    // evaluates things the real Postgres provider cannot translate. These tests close that gap: each query the
    // Project Management module relies on is built against the real Npgsql provider and turned into SQL with
    // ToQueryString(), which fails loudly if EF Core cannot translate it. No database connection is opened.
    public class PmQueryTranslationTests : IDisposable
    {
        private readonly ProjectManagementContext _db = new(
            new DbContextOptionsBuilder<ProjectManagementContext>().UseNpgsql("Host=localhost;Database=never_connected;Username=x;Password=x").Options);

        public void Dispose() => _db.Dispose();

        private static void AssertSql(string sql, params string[] fragments)
        {
            Assert.False(string.IsNullOrWhiteSpace(sql));
            foreach (var fragment in fragments)
                Assert.Contains(fragment, sql, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void BacklogFilter_WithEveryOption_TranslatesToSql()
        {
            var filter = new WorkItemFilterDto
            {
                Type = "Story", Status = "todo", AssigneeId = 4, ParentId = 2, SprintId = 7, Unplanned = true, LabelId = 9, Search = "Login",
            };

            var sql = PmWorkItemService.Project(PmWorkItemService.ApplyFilter(_db.Tasks.Where(t => t.ProjectId == 1), filter)).ToQueryString();

            AssertSql(sql, "TaskTags", "lower(", "LIKE");   // label filter, labels projection, case-insensitive search
        }

        [Fact]
        public void WorkItemProjection_WithLabelsAndNoFilter_TranslatesToSql() =>
            AssertSql(PmWorkItemService.Project(_db.Tasks).ToQueryString(), "Tags", "HasExplicitDueDate");

        [Fact]
        public void OverviewRows_Translate() =>
            AssertSql(PmInsightsService.OverviewRowsQuery(_db.Tasks, 1).ToQueryString(), "HasExplicitDueDate", "Users");

        [Fact]
        public void RecentActivity_TranslatesWithItsIdListAndLimit() =>
            AssertSql(PmInsightsService.RecentActivityQuery(_db.ActivityLogEntries, 1, new[] { 1, 2, 3 }, 10).ToQueryString(), "ORDER BY", "LIMIT");

        [Fact]
        public void ItemTimeline_Queries_Translate()
        {
            AssertSql(PmInsightsService.ItemLogQuery(_db.ActivityLogEntries, 5).ToQueryString(), "WorkItem");
            AssertSql(PmInsightsService.ItemTransitionsQuery(_db.TaskStatusHistories, 5).ToQueryString(), "IS NOT NULL");
        }

        [Fact]
        public void MyWork_JoinsProjectAndSprintNames() =>
            AssertSql(PmInsightsService.MyWorkQuery(_db.Tasks, 10).ToQueryString(), "Projects", "Sprints");

        [Fact]
        public void LabelQueries_Translate()
        {
            AssertSql(PmLabelService.ProjectLabelsQuery(_db.TaskTags, 1).ToQueryString(), "DISTINCT");
            AssertSql(PmLabelService.FindTagsQuery(_db.Tags, new[] { "ui", "api" }).ToQueryString(), "lower(");
            AssertSql(PmLabelService.CurrentLabelsQuery(_db.TaskTags, 1).ToQueryString(), "Tags");
            AssertSql(PmLabelService.OrphanedTagsQuery(_db.Tags, new[] { 1, 2 }).ToQueryString(), "NOT EXISTS");
        }

        [Fact]
        public void ReportQueries_Translate()
        {
            AssertSql(PmReportService.TransitionsQuery(_db.TaskStatusHistories, new[] { 1, 2 }, DateTime.UtcNow.AddDays(-30)).ToQueryString(), "ChangedAt");
            AssertSql(PmReportService.WorkloadQuery(_db.Tasks, DateTime.UtcNow.AddDays(-30)).ToQueryString(), "CompletedAt");
        }

        [Fact]
        public void MemberImportQueries_Translate()
        {
            AssertSql(PmProjectService.UsersByEmailQuery(_db.Users, new[] { "dana@x.com" }).ToQueryString(), "lower(");
            AssertSql(PmProjectService.PermissionCountsQuery(_db.UserRoles, new[] { 1, 2 }).ToQueryString(), "COUNT(");
        }

        [Fact]
        public void ProjectAdminLookup_JoinsTheUserAndFiltersOnTheirFlags() =>
            AssertSql(ProjectAccessService.ProjectAdminsQuery(_db.ProjectUsers, 7).ToQueryString(), "PmAccess", "PmReadOnly", "IsActive");

        [Fact]
        public void PmAttachmentFilter_BecomesAPrefixMatch() =>
            AssertSql(PmCollaborationService.PmAttachmentsQuery(_db.Attachments).ToQueryString(), "LIKE 'pm/%'");
    }
}
