using System.Net;
using System.Net.Http.Json;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.IntegrationTests.Infrastructure;
using Xunit;

namespace KhoiProjectManagement.FunctionalTests
{
    // Rich-text descriptions, subtasks and per-person effort over real HTTP and a real database.
    [Collection("Api")]
    public class PmEffortControllerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public PmEffortControllerTests(PostgresContainerFixture fixture) => _fixture = fixture;

        private sealed record Setup(HttpClient Admin, HttpClient Member, int AdminId, int ProjectId, int ItemId, int SprintId);

        private async Task<Setup> ArrangeAsync()
        {
            var admin = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Admin);
            var member = await _fixture.Factory.CreateClient().AuthenticateAsAsync(SeededUsers.Member);
            var adminId = (await admin.GetFromJsonAsync<MeResponseDto>("/api/auth/me"))!.User.Id;

            var project = await (await admin.PostAsJsonAsync("/api/projects", new
            {
                name = $"Effort {Guid.NewGuid():N}", description = "d", priority = "medium",
                startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date.AddDays(30), teamMemberIds = new[] { adminId },
            })).Content.ReadFromJsonAsync<ProjectDto>();

            var sprint = await (await admin.PostAsJsonAsync($"/api/pm/projects/{project!.Id}/sprints", new
            {
                name = "Sprint 1", goal = "g", startDate = DateTime.UtcNow.Date, endDate = DateTime.UtcNow.Date.AddDays(14),
            })).Content.ReadFromJsonAsync<SprintDto>();

            var itemResponse = await admin.PostAsJsonAsync($"/api/pm/projects/{project.Id}/work-items", new
            {
                title = "Login", description = "<p>Plan <strong>it</strong></p><script>alert(1)</script>", type = "Story", status = "todo", priority = "medium",
            });
            itemResponse.EnsureSuccessStatusCode();
            var item = await itemResponse.Content.ReadFromJsonAsync<WorkItemDto>();

            (await admin.PutAsJsonAsync($"/api/pm/work-items/{item!.Id}/sprint", new { sprintId = sprint!.Id })).EnsureSuccessStatusCode();
            return new Setup(admin, member, adminId, project.Id, item.Id, sprint.Id);
        }

        [Fact]
        public async Task TheDescriptionIsStoredAsSafeRichText_NeverScript()
        {
            var s = await ArrangeAsync();

            var item = await s.Admin.GetFromJsonAsync<WorkItemDto>($"/api/pm/work-items/{s.ItemId}");

            Assert.Equal("<p>Plan <strong>it</strong></p>", item!.Description);
        }

        [Fact]
        public async Task EffortIsAllocatedPerPersonAndCategory_AndNaIsKeptApartFromZero()
        {
            var s = await ArrangeAsync();

            var saved = await s.Admin.PutAsJsonAsync($"/api/pm/work-items/{s.ItemId}/effort", new
            {
                people = new[] { new { userId = s.AdminId, cells = new object[]
                {
                    new { category = "AnalysisDesign", points = 12m }, new { category = "DevEffort", points = 30m },
                    new { category = "Demo", points = 0.5m }, new { category = "Training", points = (decimal?)null },
                } } },
            });
            saved.EnsureSuccessStatusCode();

            var effort = await s.Admin.GetFromJsonAsync<WorkItemEffortDto>($"/api/pm/work-items/{s.ItemId}/effort");
            var person = Assert.Single(effort!.People);
            Assert.Equal(42.5m, person.Total);
            Assert.Null(person.Cells.Single(c => c.Category == "Training").Points);
        }

        [Fact]
        public async Task TheSprintSumsPointsPerPerson_WithTheItemsBehindThem()
        {
            var s = await ArrangeAsync();
            await s.Admin.PutAsJsonAsync($"/api/pm/work-items/{s.ItemId}/effort", new
            {
                people = new[] { new { userId = s.AdminId, cells = new[] { new { category = "DevEffort", points = (decimal?)8m } } } },
            });

            var sprint = await s.Admin.GetFromJsonAsync<SprintEffortDto>($"/api/pm/sprints/{s.SprintId}/effort");

            var person = Assert.Single(sprint!.People);
            Assert.Equal(8m, person.Total);
            Assert.Equal("Login", Assert.Single(person.Items).Title);
        }

        [Fact]
        public async Task EffortCannotGoToSomeoneWhoIsNotOnTheProject()
        {
            var s = await ArrangeAsync();
            var memberId = (await s.Member.GetFromJsonAsync<MeResponseDto>("/api/auth/me"))!.User.Id;

            var response = await s.Admin.PutAsJsonAsync($"/api/pm/work-items/{s.ItemId}/effort", new
            {
                people = new[] { new { userId = memberId, cells = new[] { new { category = "DevEffort", points = (decimal?)1m } } } },
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task AMemberNotOnTheProject_CannotReadOrChangeItsEffort()
        {
            var s = await ArrangeAsync();

            Assert.NotEqual(HttpStatusCode.OK, (await s.Member.GetAsync($"/api/pm/work-items/{s.ItemId}/effort")).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await s.Member.GetAsync($"/api/pm/sprints/{s.SprintId}/effort")).StatusCode);
            var put = await s.Member.PutAsJsonAsync($"/api/pm/work-items/{s.ItemId}/effort", new { people = Array.Empty<object>() });
            Assert.NotEqual(HttpStatusCode.OK, put.StatusCode);
        }
    }
}
