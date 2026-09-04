using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // CommunicationsController: broadcast (email.broadcast) and system-overview-email-settings
    // (email.manage_overview) are independently gated per-action, not by a shared class-level policy.
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

        [Fact]
        public async Task GetSystemOverviewEmailSettings_AsMemberWithoutPermission_Returns403()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);

            var response = await client.GetAsync("/api/communications/system-overview-email-settings");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetSystemOverviewEmailSettings_AsAdmin_ReturnsTheSeededDefault()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/communications/system-overview-email-settings");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var settings = await response.Content.ReadFromJsonAsync<SystemOverviewEmailSettingsDto>();
            Assert.True(settings!.Enabled);
            Assert.Equal(DayOfWeek.Friday, settings.DayOfWeek);
            Assert.Equal(10, settings.Hour);
        }

        [Fact]
        public async Task UpdateSystemOverviewEmailSettings_AsAdmin_PersistsAndReturnsTheChange()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.PutAsJsonAsync("/api/communications/system-overview-email-settings", new
            {
                enabled = false,
                dayOfWeek = DayOfWeek.Monday,
                hour = 9,
                minute = 15
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var settings = await response.Content.ReadFromJsonAsync<SystemOverviewEmailSettingsDto>();
            Assert.False(settings!.Enabled);
            Assert.Equal(DayOfWeek.Monday, settings.DayOfWeek);
            Assert.Equal(9, settings.Hour);
            Assert.Equal(15, settings.Minute);
            Assert.Equal("Kolisa Mjobo", settings.UpdatedByUserName);
        }

        [Fact]
        public async Task UpdateSystemOverviewEmailSettings_WithAnOutOfRangeHour_Returns400()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.PutAsJsonAsync("/api/communications/system-overview-email-settings", new
            {
                enabled = true,
                dayOfWeek = DayOfWeek.Friday,
                hour = 25,
                minute = 0
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
