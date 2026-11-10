using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class RichTextTests
    {
        [Theory]
        [InlineData("<p>ok</p><script>alert(1)</script>", "<p>ok</p>")]
        [InlineData("<p onclick=\"x()\">hi</p>", "<p>hi</p>")]
        [InlineData("<img src=x onerror=alert(1)>", "")]
        [InlineData("<iframe src=\"https://evil\"></iframe><b>bold</b>", "<b>bold</b>")]
        public void ScriptAndUnsafeMarkup_IsStripped(string input, string expected) =>
            Assert.Equal(expected, RichText.Sanitize(input));

        [Fact]
        public void LinksKeepOnlySafeAddresses_AndOpenInANewTab()
        {
            Assert.DoesNotContain("javascript", RichText.Sanitize("<a href=\"javascript:alert(1)\">x</a>"));
            var ok = RichText.Sanitize("<a href=\"https://example.com/a\">x</a>");
            Assert.Contains("href=\"https://example.com/a\"", ok);
            Assert.Contains("rel=\"noopener noreferrer\"", ok);
            Assert.Contains("target=\"_blank\"", ok);
        }

        [Fact]
        public void FormattingTheEditorProduces_IsKept()
        {
            const string html = "<h2>Goal</h2><p><strong>Bold</strong> <em>it</em> <u>u</u></p><ul><li>one</li><li>two</li></ul><ol><li>a</li></ol>";
            Assert.Equal(html, RichText.Sanitize(html));
        }

        [Fact]
        public void EmptyInput_GivesEmptyText()
        {
            Assert.Equal("", RichText.Sanitize(null));
            Assert.Equal("", RichText.Sanitize("   "));
        }
    }

    public class PmEffortServiceTests
    {
        private const int Item = 10, Project = 1, Sprint = 3;
        private readonly PmCaller _caller = new(7, false, false);
        private readonly List<WorkItemEffort> _store = new();
        private readonly IRepository<WorkItemEffort> _efforts = Substitute.For<IRepository<WorkItemEffort>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();

        private PmEffortService Sut(params int[] memberIds)
        {
            _efforts.Query().Returns(_ => _store.BuildMock());
            _efforts.When(r => r.Add(Arg.Any<WorkItemEffort>())).Do(c => _store.Add(c.Arg<WorkItemEffort>()));
            _efforts.When(r => r.RemoveRange(Arg.Any<IEnumerable<WorkItemEffort>>())).Do(c => { foreach (var e in c.Arg<IEnumerable<WorkItemEffort>>().ToList()) _store.Remove(e); });

            var users = new Dictionary<int, User> { [1] = new() { Id = 1, Name = "Ada" }, [2] = new() { Id = 2, Name = "Ben" } };
            // Effort rows get their navigation properties the way EF would fill them.
            _efforts.When(r => r.Add(Arg.Any<WorkItemEffort>())).Do(c =>
            {
                var e = c.Arg<WorkItemEffort>();
                e.User = users[e.UserId];
                e.Task = Tasks().First(t => t.Id == e.TaskId);
            });

            var tasks = Substitute.For<IRepository<ProjectTask>>();
            tasks.Query().Returns(_ => Tasks().BuildMock());
            var sprints = Substitute.For<IRepository<Sprint>>();
            sprints.Query().Returns(_ => new List<Sprint> { new() { Id = Sprint, ProjectId = Project, Name = "Sprint 1" } }.BuildMock());
            var members = Substitute.For<IRepository<ProjectUser>>();
            members.Query().Returns(_ => memberIds.Select(id => new ProjectUser { ProjectId = Project, UserId = id }).ToList().BuildMock());
            return new PmEffortService(_efforts, tasks, sprints, members, _access, Substitute.For<IActivityLogService>(), Substitute.For<IUnitOfWork>());
        }

        private static List<ProjectTask> Tasks()
        {
            var story = new ProjectTask { Id = 10, ProjectId = Project, Title = "Login", Type = "Story", Status = "completed", SprintId = Sprint };
            return new List<ProjectTask>
            {
                story,
                new() { Id = 11, ProjectId = Project, Title = "Wire API", Type = "Subtask", Status = "todo", SprintId = Sprint, ParentId = 10, Parent = story },
                new() { Id = 12, ProjectId = Project, Title = "Nobody yet", Type = "Task", Status = "todo", SprintId = Sprint },
                new() { Id = 99, ProjectId = Project, Title = "Other sprint", Type = "Task", Status = "todo", SprintId = 4 },
            };
        }

        private static SavePersonEffortDto Person(int userId, params (string Category, decimal? Points)[] cells) =>
            new() { UserId = userId, Cells = cells.Select(c => new EffortCellDto { Category = c.Category, Points = c.Points }).ToList() };

        [Fact]
        public async Task SavingEffort_StoresPerPersonPerCategory_AndNaIsNotZero()
        {
            var sut = Sut(1, 2);

            var saved = await sut.SetItemEffortAsync(_caller, Item, new SaveWorkItemEffortDto
            {
                People = { Person(1, (EffortCategories.AnalysisDesign, 12), (EffortCategories.DevEffort, 30), (EffortCategories.Demo, 0.5m), (EffortCategories.Training, null)) },
            });

            var ada = Assert.Single(saved.People);
            Assert.Equal(42.5m, ada.Total);
            Assert.Null(ada.Cells.Single(c => c.Category == EffortCategories.Training).Points);   // N/A
            Assert.Equal(0.5m, ada.Cells.Single(c => c.Category == EffortCategories.Demo).Points);
        }

        [Fact]
        public async Task SavingAgain_ReplacesWhatWasThere()
        {
            var sut = Sut(1, 2);
            await sut.SetItemEffortAsync(_caller, Item, new SaveWorkItemEffortDto { People = { Person(1, (EffortCategories.DevEffort, 5)) } });

            var saved = await sut.SetItemEffortAsync(_caller, Item, new SaveWorkItemEffortDto { People = { Person(2, (EffortCategories.DevTesting, 3)) } });

            Assert.Equal("Ben", Assert.Single(saved.People).UserName);
        }

        [Fact]
        public async Task EffortCanOnlyGoToSomeoneOnTheProject()
        {
            var sut = Sut(1);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.SetItemEffortAsync(_caller, Item, new SaveWorkItemEffortDto { People = { Person(2, (EffortCategories.DevEffort, 1)) } }));
        }

        [Theory]
        [InlineData("Coffee", 1)]
        [InlineData(EffortCategories.DevEffort, -1)]
        [InlineData(EffortCategories.DevEffort, 5000)]
        public async Task BadCategoriesAndAmounts_AreRejected(string category, double points)
        {
            var sut = Sut(1);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.SetItemEffortAsync(_caller, Item, new SaveWorkItemEffortDto { People = { Person(1, (category, (decimal)points)) } }));
        }

        [Fact]
        public async Task SavingRequiresEditRights()
        {
            var sut = Sut(1);
            _access.RequireAsync(_caller, Project, PmAction.EditItem).Returns(Task.FromException(new ForbiddenException("no")));

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                sut.SetItemEffortAsync(_caller, Item, new SaveWorkItemEffortDto { People = { Person(1, (EffortCategories.DevEffort, 1)) } }));
        }

        [Fact]
        public async Task TheSprintTotalsArePerPerson_WithAnItemAndCategoryDrillDown()
        {
            var sut = Sut(1, 2);
            await sut.SetItemEffortAsync(_caller, 10, new SaveWorkItemEffortDto { People = { Person(1, (EffortCategories.AnalysisDesign, 12), (EffortCategories.DevEffort, 30)) } });
            await sut.SetItemEffortAsync(_caller, 11, new SaveWorkItemEffortDto { People = { Person(1, (EffortCategories.DevTesting, 3)), Person(2, (EffortCategories.DevEffort, 8)) } });

            var sprint = await sut.GetSprintEffortAsync(_caller, Sprint);

            var ada = sprint.People.Single(p => p.UserName == "Ada");
            var ben = sprint.People.Single(p => p.UserName == "Ben");
            Assert.Equal(45m, ada.Total);                // 12 + 30 on the story, 3 on the subtask
            Assert.Equal(42m, ada.Done);                 // only the completed story's points are done
            Assert.Equal(8m, ben.Total);
            Assert.Equal(new[] { "Login", "Wire API" }, ada.Items.Select(i => i.Title));   // drill-down: items, biggest first
            Assert.Equal("Login", ada.Items[1].ParentTitle);
            Assert.Equal(new[] { EffortCategories.AnalysisDesign, EffortCategories.DevEffort },
                ada.Items[0].Cells.Select(c => c.Category));                               // ...then categories
            Assert.Equal(53m, sprint.Total);
            Assert.Equal(new[] { "Nobody yet" }, sprint.Unallocated.Select(i => i.Title)); // the other-sprint item is not listed
        }
    }
}
