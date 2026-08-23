using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    [Collection("Api")]
    public class UsersControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public UsersControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task GetUsers_ReturnsTheSeededUsers()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/users");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var users = await response.Content.ReadFromJsonAsync<List<TeamMemberDto>>();
            Assert.Contains(users!, u => u.Email.Equals(SeededUsers.Admin.Email, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task GetUser_WithNonexistentId_Returns404()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/users/999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateUser_AsMemberWithoutPermission_Returns403()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);

            var response = await client.PostAsJsonAsync("/api/users", new
            {
                name = "Should Be Denied",
                email = $"denied-{Guid.NewGuid():N}@khoitech.africa",
                role = "member",
                position = "QA",
                password = "SomeLongPassword1!"
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateUser_AsAdmin_Returns201_ThenDuplicateEmailReturns400()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var email = $"functest-user-{Guid.NewGuid():N}@khoitech.africa";
            var payload = new { name = "Func Test User", email, role = "member", position = "QA", password = "SomeLongPassword1!" };

            var first = await client.PostAsJsonAsync("/api/users", payload);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var second = await client.PostAsJsonAsync("/api/users", payload);
            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task AssignRoles_AsAdmin_UpdatesTheUsersRoles()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var email = $"functest-roles-{Guid.NewGuid():N}@khoitech.africa";
            var created = await (await client.PostAsJsonAsync("/api/users", new
            {
                name = "Role Assignee",
                email,
                role = "member",
                position = "QA",
                password = "SomeLongPassword1!"
            })).Content.ReadFromJsonAsync<TeamMemberDto>();

            var response = await client.PutAsJsonAsync($"/api/users/{created!.Id}/roles", new { roleIds = new[] { 3 } }); // seeded "Member" role

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task DeactivateUser_AsMember_Returns403_AsAdmin_Returns204()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var created = await (await adminClient.PostAsJsonAsync("/api/users", new
            {
                name = "To Be Deactivated",
                email = $"functest-deactivate-{Guid.NewGuid():N}@khoitech.africa",
                role = "member",
                position = "QA",
                password = "SomeLongPassword1!"
            })).Content.ReadFromJsonAsync<TeamMemberDto>();

            var memberClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);
            var denied = await memberClient.DeleteAsync($"/api/users/{created!.Id}");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            var allowed = await adminClient.DeleteAsync($"/api/users/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        }
    }
}
