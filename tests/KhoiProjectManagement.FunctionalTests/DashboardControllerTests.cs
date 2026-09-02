using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // Exercises DashboardController -> DashboardService -> the new Dapper-backed
    // IDashboardStatsRepository against a real Postgres container - the only way to actually prove the
    // hand-written SQL is correct, since Dapper has no LINQ compile-time checking. The shared "Api"
    // collection database accumulates rows across every test class, so assertions here are BEFORE/AFTER
    // deltas around data this test creates itself, never absolute totals.
    [Collection("Api")]
    public class DashboardControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public DashboardControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task GetStatistics_ReflectsProjectsAndTasksJustCreated()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var before = await client.GetFromJsonAsync<DashboardStatisticsDto>("/api/dashboard/statistics");

            var project = await (await client.PostAsJsonAsync("/api/projects", new
            {
                name = $"Dashboard Func Project {Guid.NewGuid():N}",
                description = "hosts tasks for DashboardControllerTests",
                priority = "medium",
                startDate = DateTime.UtcNow.Date,
                endDate = DateTime.UtcNow.Date.AddDays(30)
            })).Content.ReadFromJsonAsync<ProjectDto>();

            var completedTask = await (await client.PostAsJsonAsync("/api/tasks", new
            {
                projectId = project!.Id,
                title = "Completed task",
                description = "func test",
                priority = "medium",
                dueDate = DateTime.UtcNow.Date.AddDays(7)
            })).Content.ReadFromJsonAsync<TaskDto>();
            await client.PutAsJsonAsync($"/api/tasks/{completedTask!.Id}/status", "completed");

            // Left at the default "todo" status with a past due date - both "todo" and "overdue" at once,
            // same as the app's real status model (only "completed" excludes "overdue").
            await client.PostAsJsonAsync("/api/tasks", new
            {
                projectId = project.Id,
                title = "Overdue task",
                description = "func test",
                priority = "medium",
                dueDate = DateTime.UtcNow.AddDays(-1)
            });

            var after = await client.GetFromJsonAsync<DashboardStatisticsDto>("/api/dashboard/statistics");

            Assert.Equal(before!.TotalProjects + 1, after!.TotalProjects);
            Assert.Equal(before.ActiveProjects + 1, after.ActiveProjects); // new projects default to "active"
            Assert.Equal(before.TotalTasks + 2, after.TotalTasks);
            Assert.Equal(before.CompletedTasks + 1, after.CompletedTasks);
            Assert.Equal(before.OverdueTasks + 1, after.OverdueTasks);
        }
    }
}
