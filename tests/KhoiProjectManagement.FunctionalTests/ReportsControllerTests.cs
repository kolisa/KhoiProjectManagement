using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // Exercises ReportsController -> ReportService -> the new Dapper-backed IReportStatsRepository
    // against a real Postgres container. The shared "Api" collection database accumulates rows across
    // every test class, so these tests find their own project/user by a unique generated name rather
    // than asserting on report totals, which would be polluted by other tests' data.
    [Collection("Api")]
    public class ReportsControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ReportsControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task ProjectSummary_ComputesTasksCountAndCompletionRateForANewProject()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var projectName = $"Report Func Project {Guid.NewGuid():N}";

            var project = await (await client.PostAsJsonAsync("/api/projects", new
            {
                name = projectName,
                description = "hosts tasks for ReportsControllerTests",
                priority = "medium",
                startDate = DateTime.UtcNow.Date,
                endDate = DateTime.UtcNow.Date.AddDays(30)
            })).Content.ReadFromJsonAsync<ProjectDto>();

            var task1 = await (await client.PostAsJsonAsync("/api/tasks", new
            {
                projectId = project!.Id,
                title = "Task 1",
                description = "func test",
                priority = "medium",
                dueDate = DateTime.UtcNow.Date.AddDays(7)
            })).Content.ReadFromJsonAsync<TaskDto>();
            await client.PutAsJsonAsync($"/api/tasks/{task1!.Id}/status", "completed");

            await client.PostAsJsonAsync("/api/tasks", new
            {
                projectId = project.Id,
                title = "Task 2",
                description = "func test",
                priority = "medium",
                dueDate = DateTime.UtcNow.Date.AddDays(7)
            });

            var report = await client.GetFromJsonAsync<ProjectSummaryReportDto>("/api/reports/project-summary");

            var summary = Assert.Single(report!.Projects, p => p.Name == projectName);
            Assert.Equal("active", summary.Status);
            Assert.Equal(2, summary.TasksCount);
            Assert.Equal(1, summary.CompletedTasks);
            Assert.Equal(50, summary.CompletionRate);
        }

        [Fact]
        public async Task TeamPerformance_ComputesAssignedCompletedAndOverdueForANewActiveUser()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var memberName = $"Report Func Member {Guid.NewGuid():N}";
            var member = await (await client.PostAsJsonAsync("/api/users", new
            {
                name = memberName,
                email = $"reportfunc-{Guid.NewGuid():N}@khoitech.africa",
                role = "member",
                position = "QA",
                password = "SomeLongPassword1!"
            })).Content.ReadFromJsonAsync<TeamMemberDto>();

            var project = await (await client.PostAsJsonAsync("/api/projects", new
            {
                name = $"Report Func Project For Member {Guid.NewGuid():N}",
                description = "hosts tasks for ReportsControllerTests",
                priority = "medium",
                startDate = DateTime.UtcNow.Date,
                endDate = DateTime.UtcNow.Date.AddDays(30)
            })).Content.ReadFromJsonAsync<ProjectDto>();

            var completedTask = await (await client.PostAsJsonAsync("/api/tasks", new
            {
                projectId = project!.Id,
                title = "Assigned completed task",
                description = "func test",
                priority = "medium",
                assignedToId = member!.Id,
                dueDate = DateTime.UtcNow.Date.AddDays(7)
            })).Content.ReadFromJsonAsync<TaskDto>();
            await client.PutAsJsonAsync($"/api/tasks/{completedTask!.Id}/status", "completed");

            // Left "todo" with a past due date - counts as assigned + overdue, not completed.
            await client.PostAsJsonAsync("/api/tasks", new
            {
                projectId = project.Id,
                title = "Assigned overdue task",
                description = "func test",
                priority = "medium",
                assignedToId = member.Id,
                dueDate = DateTime.UtcNow.AddDays(-1)
            });

            var report = await client.GetFromJsonAsync<TeamPerformanceReportDto>("/api/reports/team-performance");

            var performance = Assert.Single(report!.TeamMembers, m => m.Name == memberName);
            Assert.Equal(2, performance.AssignedTasks);
            Assert.Equal(1, performance.CompletedTasks);
            Assert.Equal(1, performance.OverdueTasks);
            Assert.Equal(50, performance.CompletionRate);
        }

        [Fact]
        public async Task TeamPerformance_ExcludesDeactivatedUsers()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var memberName = $"Report Func Deactivated Member {Guid.NewGuid():N}";
            var member = await (await client.PostAsJsonAsync("/api/users", new
            {
                name = memberName,
                email = $"reportfunc-deactivated-{Guid.NewGuid():N}@khoitech.africa",
                role = "member",
                position = "QA",
                password = "SomeLongPassword1!"
            })).Content.ReadFromJsonAsync<TeamMemberDto>();

            var deactivate = await client.DeleteAsync($"/api/users/{member!.Id}");
            deactivate.EnsureSuccessStatusCode();

            var report = await client.GetFromJsonAsync<TeamPerformanceReportDto>("/api/reports/team-performance");

            Assert.DoesNotContain(report!.TeamMembers, m => m.Name == memberName);
        }
    }
}
