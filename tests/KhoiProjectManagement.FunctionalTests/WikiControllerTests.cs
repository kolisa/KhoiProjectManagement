using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // The third Space-scoped consumer (after Vault/Wiki in the plan this suite follows) - same
    // resource-based SpacePermissionAuthorizationHandler path, different resource type.
    [Collection("Api")]
    public class WikiControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public WikiControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        private async Task<int> CreateWikiSpaceAsAdminAsync(HttpClient adminClient)
        {
            var response = await adminClient.PostAsJsonAsync("/api/spaces", new
            {
                name = $"Functest Wiki Space {Guid.NewGuid():N}",
                spaceType = "WikiSpace",
                inheritPermissions = true
            });
            response.EnsureSuccessStatusCode();
            var space = await response.Content.ReadFromJsonAsync<SpaceDto>();
            return space!.Id; // Admin (creator) gets an automatic Manage grant - see SpaceService.CreateSpaceAsync.
        }

        [Fact]
        public async Task CreatePage_AsUserWithNoGrantOnTheSpace_Returns403()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var spaceId = await CreateWikiSpaceAsAdminAsync(adminClient);

            var memberClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);
            var response = await memberClient.PostAsJsonAsync("/api/wiki/pages", new
            {
                title = "Should Be Denied",
                spaceId,
                contentMarkdown = "# denied"
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreatePage_AsCreatorOfTheSpace_Returns201AndIsRetrievable()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var spaceId = await CreateWikiSpaceAsAdminAsync(adminClient);

            var createResponse = await adminClient.PostAsJsonAsync("/api/wiki/pages", new
            {
                title = "Functest Page",
                spaceId,
                contentMarkdown = "# Hello from a functional test"
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var page = await createResponse.Content.ReadFromJsonAsync<WikiPageDetailDto>();

            var getResponse = await adminClient.GetAsync($"/api/wiki/pages/{page!.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<WikiPageDetailDto>();
            Assert.Equal("Functest Page", fetched!.Title);
        }

        [Fact]
        public async Task GetPage_WithNonexistentId_Returns404()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/wiki/pages/999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task DeletePage_AsCreatorOfTheSpace_Returns204ThenIsGone()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var spaceId = await CreateWikiSpaceAsAdminAsync(adminClient);
            var page = await (await adminClient.PostAsJsonAsync("/api/wiki/pages", new
            {
                title = "To Be Deleted",
                spaceId,
                contentMarkdown = "# bye"
            })).Content.ReadFromJsonAsync<WikiPageDetailDto>();

            var deleteResponse = await adminClient.DeleteAsync($"/api/wiki/pages/{page!.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var afterDelete = await adminClient.GetAsync($"/api/wiki/pages/{page.Id}");
            Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        }
    }
}
