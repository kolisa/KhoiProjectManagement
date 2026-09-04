using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // CommunicationsController is gated by a single class-level [Authorize(Policy = "email.broadcast")].
    [Collection("Api")]
    public class CommunicationsControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public CommunicationsControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task SendBroadcast_AsMemberWithoutPermission_Returns403()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);

            var response = await client.PostAsJsonAsync("/api/communications/broadcast", new
            {
                subject = "Should be denied",
                body = "Members can't send broadcasts.",
                roleIds = new[] { 3 }
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task SendBroadcast_AsAdmin_Returns200WithRecipientCount()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            // Seeded Role 3 (Member) has at least the seeded member users - see DatabaseSeeder.
            var response = await client.PostAsJsonAsync("/api/communications/broadcast", new
            {
                subject = "Functional test broadcast",
                body = "Line one\nLine two",
                roleIds = new[] { 3 }
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<BroadcastEmailResultDto>();
            Assert.True(result!.RecipientCount > 0);
        }

        [Fact]
        public async Task SendBroadcast_WithNoRolesSelected_Returns400()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.PostAsJsonAsync("/api/communications/broadcast", new
            {
                subject = "Missing roles",
                body = "Should fail validation.",
                roleIds = Array.Empty<int>()
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
