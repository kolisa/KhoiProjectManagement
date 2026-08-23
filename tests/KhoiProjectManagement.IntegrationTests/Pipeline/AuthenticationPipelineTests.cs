using System.Net;
using System.Net.Http.Headers;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.IntegrationTests.Pipeline
{
    // Proves the JWT authentication pipeline itself (middleware wiring, not any one controller's logic)
    // rejects bad tokens before a request ever reaches a controller action.
    [Collection("Api")]
    public class AuthenticationPipelineTests
    {
        private readonly PostgresContainerFixture _fixture;

        public AuthenticationPipelineTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task ProtectedEndpoint_WithNoAuthorizationHeader_Returns401()
        {
            var client = _fixture.Factory.CreateClient();

            var response = await client.GetAsync("/api/projects");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ProtectedEndpoint_WithMalformedToken_Returns401()
        {
            var client = _fixture.Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "this-is-not-a-jwt");

            var response = await client.GetAsync("/api/projects");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ProtectedEndpoint_WithValidToken_Returns200()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var response = await client.GetAsync("/api/projects");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
