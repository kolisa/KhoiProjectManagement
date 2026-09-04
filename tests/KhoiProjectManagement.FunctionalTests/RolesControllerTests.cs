using System.Linq;
using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // RolesController is gated by a single class-level [Authorize(Policy = "users.manage_roles")] -
    // every action should behave identically for a caller without it.
    [Collection("Api")]
    public class RolesControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public RolesControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task GetRoles_AsMemberWithoutPermission_Returns403()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);

            var response = await client.GetAsync("/api/roles");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetRoles_AsAdmin_ReturnsTheSeededRoles()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/roles");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var roles = await response.Content.ReadFromJsonAsync<List<RoleDto>>();
            Assert.Contains(roles!, r => r.Name == "Admin");
            Assert.Contains(roles!, r => r.Name == "Member");
        }

        [Fact]
        public async Task GetPermissions_AsAdmin_ReturnsAllSeededPermissions()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/permissions");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var permissions = await response.Content.ReadFromJsonAsync<List<PermissionDto>>();
            Assert.Equal(27, permissions!.Count); // DatabaseSeeder/OnModelCreating seeds exactly 27 (26 + email.broadcast).
        }

        // Role.Name is Fluent-configured HasMaxLength(50) in OnModelCreating (a real varchar(50) DB
        // constraint, unlike most other string columns which now map to unbounded text - see the
        // SyncColumnTypesToNpgsql10 migration) - keep generated names comfortably under that.
        private static string ShortRoleName(string label) => $"{label}-{Guid.NewGuid():N}"[..(label.Length + 9)];

        [Fact]
        public async Task CreateRole_ThenSetPermissions_ThenGetRolePermissions_RoundTrips()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var createResponse = await client.PostAsJsonAsync("/api/roles", new
            {
                name = ShortRoleName("FT-Role"),
                description = "created by a functional test"
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var role = await createResponse.Content.ReadFromJsonAsync<RoleDto>();

            var setResponse = await client.PutAsJsonAsync($"/api/roles/{role!.Id}/permissions", new
            {
                permissionNames = new[] { "dashboard.view" }
            });
            Assert.Equal(HttpStatusCode.NoContent, setResponse.StatusCode);

            var getResponse = await client.GetAsync($"/api/roles/{role.Id}/permissions");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            // GetRolePermissionsAsync returns List<string> (permission names), not PermissionDto objects.
            var permissionNames = await getResponse.Content.ReadFromJsonAsync<List<string>>();
            Assert.Contains("dashboard.view", permissionNames!);
        }

        [Fact]
        public async Task SetRolePermissions_WithAnUnknownPermissionName_IsSilentlyIgnoredNotAnError()
        {
            // RoleService.SetRolePermissionsAsync filters permissionNames against the seeded set and
            // simply drops anything that doesn't match - not a validation error. Regression test for
            // that exact (perhaps-surprising) contract.
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var role = await (await client.PostAsJsonAsync("/api/roles", new
            {
                name = ShortRoleName("FT-UnkPerm"),
                description = "desc"
            })).Content.ReadFromJsonAsync<RoleDto>();

            var response = await client.PutAsJsonAsync($"/api/roles/{role!.Id}/permissions", new
            {
                permissionNames = new[] { "dashboard.view", "not.a.real.permission" }
            });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var permissionNames = await (await client.GetAsync($"/api/roles/{role.Id}/permissions"))
                .Content.ReadFromJsonAsync<List<string>>();
            Assert.Single(permissionNames!);
            Assert.Equal("dashboard.view", permissionNames![0]);
        }

        [Fact]
        public async Task SetRolePermissions_WhenItWouldLockTheCallerOutOfRoleManagement_Returns400()
        {
            // Self-lockout guard: the seeded Admin user holds users.manage_roles only via the Admin
            // role - stripping it from Admin's own permission set (with no other role granting it)
            // must be rejected, not silently lock the caller out of ever managing roles again.
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var adminRole = (await (await client.GetAsync("/api/roles")).Content.ReadFromJsonAsync<List<RoleDto>>())!
                .Single(r => r.Name == "Admin");

            var response = await client.PutAsJsonAsync($"/api/roles/{adminRole.Id}/permissions", new
            {
                permissionNames = new[] { "dashboard.view" } // drops users.manage_roles
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // And the Admin role's actual permissions must be untouched by the rejected attempt.
            var stillHasManageRoles = (await (await client.GetAsync($"/api/roles/{adminRole.Id}/permissions"))
                .Content.ReadFromJsonAsync<List<string>>())!;
            Assert.Contains("users.manage_roles", stillHasManageRoles);
        }
    }
}
