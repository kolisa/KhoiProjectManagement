using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.IntegrationTests.Pipeline
{
    // Proves ValidationActionFilter (registered globally in ServiceCollectionExtensions, not per
    // controller) actually intercepts an invalid request before the action - and before the underlying
    // service/database - ever runs, and that the error contract's shape is exactly what the frontend
    // expects (KhoiProjectManagementApp/src/utils/validation.js mirrors the same field-level shape).
    [Collection("Api")]
    public class ValidationPipelineTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ValidationPipelineTests(PostgresContainerFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task CreateProject_WithMissingName_ShortCircuitsWith400AndDoesNotCreateAnything()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            var before = await client.GetFromJsonAsync<JsonElement[]>("/api/projects");

            var response = await client.PostAsJsonAsync("/api/projects", new
            {
                name = "",
                description = "missing a name",
                priority = "medium",
                startDate = DateTime.UtcNow,
                endDate = DateTime.UtcNow.AddDays(1)
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            // ValidationActionFilter prefixes each key with the action parameter's name
            // (createProjectDto) - a regression test for that exact contract, not just the status code.
            Assert.True(body.GetProperty("errors").TryGetProperty("createProjectDto.Name", out _));

            var after = await client.GetFromJsonAsync<JsonElement[]>("/api/projects");
            Assert.Equal(before!.Length, after!.Length);
        }

        [Fact]
        public async Task GetProject_WithNonNumericId_FailsModelBindingBeforeReachingTheAction()
        {
            var client = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);

            // {id} binds to `int id` - a non-numeric segment fails ASP.NET Core's own model binding
            // (via [ApiController]'s automatic ModelState validation) before ProjectsController.GetProject
            // ever runs, distinct from both ValidationActionFilter and a real 404 for a numeric-but-missing id.
            var response = await client.GetAsync("/api/projects/not-a-number");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
