using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // Exercises the resource-based SpacePermissionAuthorizationHandler path (VaultService ->
    // IAuthorizationService.AuthorizeAsync against a SpacePermissionRequirement) - a distinct
    // authorization mechanism from the flat policy checks ProjectsControllerTests exercises, both wired
    // into the same ASP.NET Core authorization pipeline per CLAUDE.md.
    [Collection("Api")]
    public class VaultControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public VaultControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        private async Task<int> CreateVaultCategorySpaceAsAdminAsync(HttpClient adminClient)
        {
            var response = await adminClient.PostAsJsonAsync("/api/spaces", new
            {
                name = $"Functest Vault Category {Guid.NewGuid():N}",
                spaceType = "VaultCategory",
                inheritPermissions = true
            });
            response.EnsureSuccessStatusCode();
            var space = await response.Content.ReadFromJsonAsync<SpaceDto>();
            return space!.Id; // Admin (creator) gets an automatic Manage grant - see SpaceService.CreateSpaceAsync.
        }

        [Fact]
        public async Task CreateEntry_AsUserWithNoGrantOnTheSpace_Returns403()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var spaceId = await CreateVaultCategorySpaceAsAdminAsync(adminClient);

            var memberClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);
            var response = await memberClient.PostAsJsonAsync("/api/vault/entries", new
            {
                name = "Should Be Denied",
                spaceId,
                secretValue = "s3cr3t"
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateEntry_AsCreatorOfTheSpace_Returns201AndRevealAndAuditWork()
        {
            var adminClient = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var spaceId = await CreateVaultCategorySpaceAsAdminAsync(adminClient);

            var createResponse = await adminClient.PostAsJsonAsync("/api/vault/entries", new
            {
                name = "Functest Secret",
                spaceId,
                secretValue = "s3cr3t-value",
                notes = "created by a functional test"
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var entry = await createResponse.Content.ReadFromJsonAsync<VaultEntryDetailDto>();

            var revealResponse = await adminClient.PostAsync($"/api/vault/entries/{entry!.Id}/reveal", content: null);
            Assert.Equal(HttpStatusCode.OK, revealResponse.StatusCode);
            var revealed = await revealResponse.Content.ReadFromJsonAsync<VaultSecretRevealDto>();
            Assert.Equal("s3cr3t-value", revealed!.SecretValue);

            var getResponse = await adminClient.GetAsync($"/api/vault/entries/{entry.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var auditResponse = await adminClient.GetAsync($"/api/vault/entries/{entry.Id}/audit");
            Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
            var auditLog = await auditResponse.Content.ReadFromJsonAsync<List<VaultAuditLogDto>>();

            var actions = auditLog!.Select(a => a.Action).ToList();
            Assert.Contains("Created", actions);
            Assert.Contains("SecretRevealed", actions);
            Assert.Contains("Viewed", actions);
        }
    }
}
