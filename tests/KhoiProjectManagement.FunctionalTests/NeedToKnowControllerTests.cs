using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // The reported leak, end to end: a signed-in user who is NOT on a project could list it, open it and create tickets in it.
    // Here an administrator makes a project and a task, and an ordinary member who is not on its team tries everything.
    [Collection("Api")]
    public class NeedToKnowControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public NeedToKnowControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        private sealed record Setup(HttpClient Admin, HttpClient Member, int MemberId, int ProjectId, int TaskId, string Marker);

        private async Task<Setup> ArrangeAsync()
        {
            var admin = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var member = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);
            var memberId = (await member.GetFromJsonAsync<MeResponseDto>("/api/auth/me"))!.User.Id;

            var marker = Guid.NewGuid().ToString("N");
            var projectResponse = await admin.PostAsJsonAsync("/api/projects", new
            {
                name = $"Secret project {marker}", description = "not the member's", priority = "medium",
                startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date.AddDays(30), teamMemberIds = Array.Empty<int>(),
            });
            projectResponse.EnsureSuccessStatusCode();
            var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDto>();

            var taskResponse = await admin.PostAsJsonAsync("/api/tasks", new
            {
                projectId = project!.Id, title = $"Secret task {marker}", description = "d", priority = "low", dueDate = DateTime.UtcNow.Date.AddDays(5),
            });
            taskResponse.EnsureSuccessStatusCode();
            var task = await taskResponse.Content.ReadFromJsonAsync<TaskDto>();

            return new Setup(admin, member, memberId, project.Id, task!.Id, marker);
        }

        [Fact]
        public async Task AMemberNotOnTheProject_CannotListItsProjectOrItsTasks()
        {
            var s = await ArrangeAsync();

            var projects = await s.Member.GetFromJsonAsync<List<ProjectDto>>("/api/projects");
            var tasks = await s.Member.GetFromJsonAsync<List<TaskDto>>("/api/tasks");

            Assert.DoesNotContain(projects!, p => p.Id == s.ProjectId);
            Assert.DoesNotContain(tasks!, t => t.Id == s.TaskId);
        }

        [Fact]
        public async Task AMemberNotOnTheProject_GetsNotFound_OpeningItsProjectTaskOrStatistics()
        {
            var s = await ArrangeAsync();

            Assert.Equal(HttpStatusCode.NotFound, (await s.Member.GetAsync($"/api/projects/{s.ProjectId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await s.Member.GetAsync($"/api/projects/{s.ProjectId}/statistics")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await s.Member.GetAsync($"/api/tasks/{s.TaskId}")).StatusCode);
        }

        [Fact]
        public async Task AMemberNotOnTheProject_CannotCreateATicketInIt()
        {
            var s = await ArrangeAsync();

            var response = await s.Member.PostAsJsonAsync("/api/tasks", new
            {
                projectId = s.ProjectId, title = "sneaky ticket", description = "d", priority = "low", dueDate = DateTime.UtcNow.Date.AddDays(3),
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var tasks = await s.Admin.GetFromJsonAsync<List<TaskDto>>($"/api/tasks?projectId={s.ProjectId}");
            Assert.DoesNotContain(tasks!, t => t.Title == "sneaky ticket");
        }

        [Fact]
        public async Task AMemberNotOnTheProject_CannotChangeItsTasks()
        {
            var s = await ArrangeAsync();

            var status = await s.Member.PutAsJsonAsync($"/api/tasks/{s.TaskId}/status", "completed");
            var update = await s.Member.PutAsJsonAsync($"/api/tasks/{s.TaskId}", new { title = "hijacked", description = "d", status = "todo", priority = "low", type = "Task", dueDate = DateTime.UtcNow.Date.AddDays(1) });

            Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
            var task = await s.Admin.GetFromJsonAsync<TaskDto>($"/api/tasks/{s.TaskId}");
            Assert.Equal("todo", task!.Status);
            Assert.StartsWith("Secret task", task.Title);
        }

        [Fact]
        public async Task SearchNeverFindsAProjectOrTaskTheMemberIsNotOn()
        {
            var s = await ArrangeAsync();

            var asMember = await s.Member.GetFromJsonAsync<GlobalSearchResultDto>($"/api/search?q={s.Marker}");
            var asAdmin = await s.Admin.GetFromJsonAsync<GlobalSearchResultDto>($"/api/search?q={s.Marker}");

            Assert.Empty(asMember!.Projects);
            Assert.Empty(asMember.Tasks);
            Assert.NotEmpty(asAdmin!.Projects);      // proves the search itself works, and an administrator still sees everything
            Assert.NotEmpty(asAdmin.Tasks);
        }

        [Fact]
        public async Task ProjectManagement_ShowsTheMemberNothingAboutAProjectTheyAreNotOn()
        {
            var s = await ArrangeAsync();

            // 404 (not 403): the project's existence is not revealed either.
            Assert.Equal(HttpStatusCode.Forbidden, (await s.Member.GetAsync("/api/pm/projects")).StatusCode);   // no module access at all
            Assert.Equal(HttpStatusCode.NotFound, (await s.Member.GetAsync($"/api/pm/projects/{s.ProjectId}/overview")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await s.Member.GetAsync($"/api/pm/work-items/{s.TaskId}/comments")).StatusCode);
        }

        [Fact]
        public async Task ThePeopleListForAddingMembers_IsNotAvailableToAnOrdinaryMember()
        {
            var s = await ArrangeAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await s.Member.GetAsync("/api/pm/users")).StatusCode);
        }

        [Fact]
        public async Task OnceAddedToTheTeam_TheMemberSeesTheProjectAndItsTasks()
        {
            var s = await ArrangeAsync();

            var update = await s.Admin.PutAsJsonAsync($"/api/projects/{s.ProjectId}", new
            {
                name = $"Secret project {s.Marker}", description = "now shared", priority = "medium", status = "active",
                startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date.AddDays(30), teamMemberIds = new[] { s.MemberId },
            });
            Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

            var projects = await s.Member.GetFromJsonAsync<List<ProjectDto>>("/api/projects");
            Assert.Contains(projects!, p => p.Id == s.ProjectId);
            Assert.Equal(HttpStatusCode.OK, (await s.Member.GetAsync($"/api/tasks/{s.TaskId}")).StatusCode);
        }

        [Fact]
        public async Task AnAdministrator_StillSeesEverything()
        {
            var s = await ArrangeAsync();

            var projects = await s.Admin.GetFromJsonAsync<List<ProjectDto>>("/api/projects");
            Assert.Contains(projects!, p => p.Id == s.ProjectId);
            Assert.Equal(HttpStatusCode.OK, (await s.Admin.GetAsync($"/api/tasks/{s.TaskId}")).StatusCode);
        }
    }
}
