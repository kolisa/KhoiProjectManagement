using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    [Collection("Api")]
    public class TasksControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public TasksControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        private static async Task<int> UserIdAsync(HttpClient client)
        {
            var me = await client.GetFromJsonAsync<MeResponseDto>("/api/auth/me");
            return me!.User.Id;
        }

        private static async Task<int> CreateProjectAsync(HttpClient adminClient, params int[] teamMemberIds)
        {
            var response = await adminClient.PostAsJsonAsync("/api/projects", new
            {
                teamMemberIds,
                name = $"Task Host Project {Guid.NewGuid():N}",
                description = "hosts tasks for TasksControllerTests",
                priority = "medium",
                startDate = DateTime.UtcNow.Date,
                endDate = DateTime.UtcNow.Date.AddDays(30)
            });
            response.EnsureSuccessStatusCode();
            var project = await response.Content.ReadFromJsonAsync<ProjectDto>();
            return project!.Id;
        }

        [Fact]
        public async Task CreateTask_AsATeamMemberOfTheProject_Returns201AndIsRetrievable()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var memberClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);

            // Need-to-know: a signed-in user can only create tickets in a project they belong to (see NeedToKnowControllerTests
            // for the other side of that). Here the member is on the team.
            var projectId = await CreateProjectAsync(adminClient, await UserIdAsync(memberClient));
            var response = await memberClient.PostAsJsonAsync("/api/tasks", new
            {
                projectId,
                title = "Func Test Task",
                description = "created by a functional test",
                priority = "medium",
                dueDate = DateTime.UtcNow.Date.AddDays(7)
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<TaskDto>();

            var fetched = await memberClient.GetFromJsonAsync<TaskDto>($"/api/tasks/{created!.Id}");
            Assert.Equal("Func Test Task", fetched!.Title);
        }

        [Fact]
        public async Task GetTask_WithNonexistentId_Returns404()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/tasks/999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateTaskStatus_ToCompleted_PersistsTheNewStatus()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var projectId = await CreateProjectAsync(adminClient);
            var created = await (await adminClient.PostAsJsonAsync("/api/tasks", new
            {
                projectId,
                title = "Status Change Task",
                description = "desc",
                priority = "low",
                dueDate = DateTime.UtcNow.Date.AddDays(7)
            })).Content.ReadFromJsonAsync<TaskDto>();

            var response = await adminClient.PutAsJsonAsync($"/api/tasks/{created!.Id}/status", "completed");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var fetched = await adminClient.GetFromJsonAsync<TaskDto>($"/api/tasks/{created.Id}");
            Assert.Equal("completed", fetched!.Status);
        }

        [Fact]
        public async Task DeleteTask_AsMember_Returns403_AsAdmin_Returns204ThenIsGone()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var projectId = await CreateProjectAsync(adminClient);
            var created = await (await adminClient.PostAsJsonAsync("/api/tasks", new
            {
                projectId,
                title = "To Be Deleted",
                description = "desc",
                priority = "low",
                dueDate = DateTime.UtcNow.Date.AddDays(7)
            })).Content.ReadFromJsonAsync<TaskDto>();

            var memberClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);
            var denied = await memberClient.DeleteAsync($"/api/tasks/{created!.Id}");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            var allowed = await adminClient.DeleteAsync($"/api/tasks/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);

            var afterDelete = await adminClient.GetAsync($"/api/tasks/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        }
    }
}
