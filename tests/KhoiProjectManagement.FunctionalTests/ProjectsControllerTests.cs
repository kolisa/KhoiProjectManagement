using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    [Collection("Api")]
    public class ProjectsControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ProjectsControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        private static object NewProjectPayload(string name) => new
        {
            name,
            description = "Created by a functional test",
            priority = "medium",
            startDate = DateTime.UtcNow.Date,
            endDate = DateTime.UtcNow.Date.AddDays(30)
        };

        [Fact]
        public async Task CreateProject_AsAdmin_Returns201WithLocationAndPersistsIt()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var name = $"Functional Project {Guid.NewGuid():N}";

            var response = await client.PostAsJsonAsync("/api/projects", NewProjectPayload(name));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);

            var created = await response.Content.ReadFromJsonAsync<ProjectDto>();
            Assert.Equal(name, created!.Name);

            var fetched = await client.GetFromJsonAsync<ProjectDto>(response.Headers.Location!.ToString());
            Assert.Equal(created.Id, fetched!.Id);
        }

        [Fact]
        public async Task CreateProject_AsMemberWithoutPermission_Returns403()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);

            var response = await client.PostAsJsonAsync("/api/projects", NewProjectPayload($"Should Be Denied {Guid.NewGuid():N}"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetProject_WithNonexistentId_Returns404()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/projects/999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeleteProject_AsMember_Returns403_AsAdmin_Returns204ThenIsGone()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var created = await (await adminClient.PostAsJsonAsync("/api/projects", NewProjectPayload($"To Be Deleted {Guid.NewGuid():N}")))
                .Content.ReadFromJsonAsync<ProjectDto>();

            var memberClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);
            var deniedDelete = await memberClient.DeleteAsync($"/api/projects/{created!.Id}");
            Assert.Equal(HttpStatusCode.Forbidden, deniedDelete.StatusCode);

            var allowedDelete = await adminClient.DeleteAsync($"/api/projects/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, allowedDelete.StatusCode);

            var afterDelete = await adminClient.GetAsync($"/api/projects/{created.Id}");
            Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        }
    }
}
