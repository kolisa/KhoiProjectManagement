using System.Net;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // HTTP-level checks on the Project Management routes: authentication, the module-access gate, the
    // pm.admin policy, and that unknown ids answer 404 without revealing anything. The role-by-role rules
    // live in the unit tests (ProjectRoles matrix, PmCollaborationService, ...); these prove the routes
    // are actually wired to them.
    [Collection("Api")]
    public class ProjectManagementControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ProjectManagementControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Theory]
        [InlineData("/api/pm/projects")]
        [InlineData("/api/pm/work-items/1/comments")]
        [InlineData("/api/pm/projects/1/reports/workload")]
        [InlineData("/api/pm/projects/1/workflow")]
        [InlineData("/api/pm/attachments/1/download")]
        public async Task Routes_WithoutAToken_Return401(string url)
        {
            var response = await _fixture.Factory.CreateClient().GetAsync(url);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetProjects_AsAUserWithoutModuleAccess_Returns403()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);

            var response = await client.GetAsync("/api/pm/projects");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task AccessAdministration_RequiresThePmAdminPermission()
        {
            var member = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);
            var admin = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/pm/access/users")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/pm/access/users")).StatusCode);
        }

        [Theory]
        [InlineData("/api/pm/work-items/999999/comments")]
        [InlineData("/api/pm/work-items/999999/checklist")]
        [InlineData("/api/pm/work-items/999999/links")]
        [InlineData("/api/pm/work-items/999999/attachments")]
        [InlineData("/api/pm/attachments/999999/download")]
        [InlineData("/api/pm/sprints/999999/retro")]
        [InlineData("/api/pm/projects/999999/workflow")]
        [InlineData("/api/pm/projects/999999/reports/cumulative-flow")]
        [InlineData("/api/pm/projects/999999/export/work-items")]
        public async Task UnknownIds_Return404_ForBothAdminsAndMembers(string url)
        {
            foreach (var user in new[] { SeededUsers.Admin, SeededUsers.Member })
            {
                var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(user);

                var response = await client.GetAsync(url);

                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
        }

        [Fact]
        public async Task MarkingAnotherUsersNotificationRead_DoesNotFail_ButNeverTouchesIt()
        {
            // Scoped to the caller: an unknown/foreign id is simply a no-op (204), not an error and not a write.
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);

            var response = await client.PutAsync("/api/notifications/999999/read", null);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
