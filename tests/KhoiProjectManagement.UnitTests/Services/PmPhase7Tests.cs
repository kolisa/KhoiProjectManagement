using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class RoleParsingTests
    {
        [Theory]
        [InlineData("Scrum Master", "ScrumMaster")]
        [InlineData("scrum-master", "ScrumMaster")]
        [InlineData("SM", "ScrumMaster")]
        [InlineData("Developer", "Developer")]
        [InlineData("dev", "Developer")]
        [InlineData("QA", "Tester")]
        [InlineData("Tester", "Tester")]
        [InlineData("Project Manager", "ProjectManager")]
        [InlineData("PM", "ProjectManager")]
        [InlineData("project_admin", "ProjectAdmin")]
        [InlineData("Read-only", "Viewer")]
        [InlineData("stakeholder", "Viewer")]
        public void Parse_UnderstandsTheWordingPeopleType(string text, string expected) =>
            Assert.Equal(expected, ProjectRoles.Parse(text));

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("wizard")]
        public void Parse_ReturnsNullWhenItCannotTell(string? text) => Assert.Null(ProjectRoles.Parse(text));

        [Fact]
        public void Parse_EveryRealRoleKeyRoundTrips() =>
            Assert.All(ProjectRoles.All, r => Assert.Equal(r, ProjectRoles.Parse(r)));
    }

    public class WorkItemRollupTests
    {
        private static WorkItemRollup.Node N(int id, int? parent, string status, int? points = null) => new(id, parent, status, points);

        [Fact]
        public void Compute_CountsOnlyLeafDescendants_AcrossLevels()
        {
            // Epic 1 > Feature 2 > Story 3 (leaf, done, 5pts) + Story 4 (leaf, todo, 3pts); Epic also has leaf Task 5 (done, 2pts)
            var result = WorkItemRollup.Compute(new[]
            {
                N(1, null, "todo"), N(2, 1, "todo"), N(3, 2, "completed", 5), N(4, 2, "todo", 3), N(5, 1, "completed", 2),
            });

            Assert.Equal(new WorkItemRollup.Result(2, 3, 7, 10), result[1]);
            Assert.Equal(new WorkItemRollup.Result(1, 2, 5, 8), result[2]);
            Assert.False(result.ContainsKey(3));  // leaves have no roll-up
        }

        [Fact]
        public void Compute_IgnoresTheContainersOwnStatus()
        {
            var result = WorkItemRollup.Compute(new[] { N(1, null, "completed", 40), N(2, 1, "todo", 3) });
            Assert.Equal(new WorkItemRollup.Result(0, 1, 0, 3), result[1]);
        }

        [Fact]
        public void Compute_WithNoChildren_ReturnsNothing() =>
            Assert.Empty(WorkItemRollup.Compute(new[] { N(1, null, "todo"), N(2, null, "completed") }));
    }

    public class PmActivityTextTests
    {
        [Theory]
        [InlineData("created", null, "created this item")]
        [InlineData("updated", "completed", "edited this item")]
        [InlineData("commented", null, "commented")]
        [InlineData("attached", "spec.pdf", "attached spec.pdf")]
        [InlineData("sprint-changed", "Sprint 3", "changed the sprint to Sprint 3")]
        [InlineData("labelled", "ui, bug", "set labels: ui, bug")]
        public void OnAnItemsTimeline_ReadsAsAShortSentence(string action, string? details, string expected) =>
            Assert.Equal(expected, PmActivityText.Describe("WorkItem", action, details, "Login page", forItem: true));

        [Fact]
        public void OnAProjectFeed_NamesTheItem() =>
            Assert.Equal("commented on \"Login page\"", PmActivityText.Describe("WorkItem", "commented", null, "Login page", forItem: false));

        [Fact]
        public void ProjectEntries_AreDescribedWithoutAnItem()
        {
            Assert.Equal("removed Sam from the project", PmActivityText.Describe("Project", "member-removed", null, "Sam", false));
            Assert.Equal("imported members: 3 added, 1 updated", PmActivityText.Describe("Project", "members-imported", "3 added, 1 updated", "Members", false));
        }
    }

    public class PmInsightsServiceTests
    {
        private static readonly PmCaller Dev = new(10, false);

        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<Sprint> _sprints = Substitute.For<IRepository<Sprint>>();
        private readonly IRepository<Project> _projects = Substitute.For<IRepository<Project>>();
        private readonly IRepository<TaskStatusHistory> _history = Substitute.For<IRepository<TaskStatusHistory>>();
        private readonly IRepository<ActivityLogEntry> _log = Substitute.For<IRepository<ActivityLogEntry>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();

        private PmInsightsService CreateSut() => new(_tasks, _sprints, _projects, _history, _log, _access);

        public PmInsightsServiceTests()
        {
            _projects.Query().Returns(_ => new List<Project> { new() { Id = 1, Name = "Khoi" } }.BuildMock());
            _sprints.Query().Returns(_ => new List<Sprint>().BuildMock());
            _history.Query().Returns(_ => new List<TaskStatusHistory>().BuildMock());
            _log.Query().Returns(_ => new List<ActivityLogEntry>().BuildMock());
            _access.HasModuleAccessAsync(default!).ReturnsForAnyArgs(true);
        }

        private void SetTasks(params ProjectTask[] t) => _tasks.Query().Returns(_ => t.ToList().BuildMock());
        private static DateTime In(int days) => DateTime.UtcNow.AddDays(days);

        [Fact]
        public async Task Overview_RequiresViewAccess()
        {
            _access.RequireAsync(Dev, 1, PmAction.View).Returns(Task.FromException(new KeyNotFoundException()));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().GetOverviewAsync(Dev, 1));
        }

        [Fact]
        public async Task Overview_SummarisesStatusRiskAndMyWork_IgnoringContainers()
        {
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Epic", Title = "E", Status = "todo", StoryPoints = 99, DueDate = In(-5) },
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Title = "Mine overdue", Status = "in-progress", Priority = "high", StoryPoints = 5, AssignedToId = 10, DueDate = In(-2) },
                new ProjectTask { Id = 3, ProjectId = 1, Type = "Task", Title = "Blocked", Status = "blocked", StoryPoints = 3, DueDate = In(30) },
                new ProjectTask { Id = 4, ProjectId = 1, Type = "Bug", Title = "Soon", Status = "todo", AssignedToId = 20, DueDate = In(3) },
                new ProjectTask { Id = 5, ProjectId = 1, Type = "Task", Title = "Done", Status = "completed", StoryPoints = 8, AssignedToId = 10, DueDate = In(-9) });

            var o = await CreateSut().GetOverviewAsync(Dev, 1);

            Assert.Equal("Khoi", o.ProjectName);
            Assert.Equal(1, o.StatusCounts["in-progress"]);
            Assert.Equal(2, o.StatusCounts["todo"] + o.StatusCounts["blocked"]);
            Assert.Equal(1, o.StatusCounts["completed"]);
            Assert.Equal(8, o.OpenPoints);                  // epic's 99 and the finished 8 are excluded
            Assert.Equal(1, o.OverdueCount);                // only "Mine overdue" (epic ignored, done ignored)
            Assert.Equal(1, o.BlockedCount);
            Assert.Equal(1, o.UnassignedCount);             // only "Blocked" is open and unassigned
            Assert.Equal("Soon", Assert.Single(o.DueSoon).Title);
            Assert.Equal("Mine overdue", Assert.Single(o.MyOpenItems).Title);
            Assert.True(o.MyOpenItems[0].IsOverdue);
        }

        [Fact]
        public async Task Overview_IgnoresPlaceholderDueDates_WhenCountingOverdueAndDueSoon()
        {
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Title = "no date, project ended", Status = "todo", DueDate = In(-30), HasExplicitDueDate = false },
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Title = "no date, ends soon", Status = "todo", DueDate = In(2), HasExplicitDueDate = false },
                new ProjectTask { Id = 3, ProjectId = 1, Type = "Task", Title = "really overdue", Status = "todo", DueDate = In(-1), HasExplicitDueDate = true });

            var o = await CreateSut().GetOverviewAsync(Dev, 1);

            Assert.Equal(1, o.OverdueCount);
            Assert.Empty(o.DueSoon);
        }

        [Fact]
        public async Task MyWork_ListsUndatedItemsLast_AndNeverCallsThemOverdue()
        {
            var p = new Project { Id = 1, Name = "Khoi" };
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Project = p, Type = "Task", Title = "undated", Status = "todo", AssignedToId = 10, DueDate = In(-30), HasExplicitDueDate = false },
                new ProjectTask { Id = 2, ProjectId = 1, Project = p, Type = "Task", Title = "dated later", Status = "todo", AssignedToId = 10, DueDate = In(20), HasExplicitDueDate = true });

            var mine = await CreateSut().GetMyWorkAsync(Dev);

            Assert.Equal(new[] { "dated later", "undated" }, mine.Select(m => m.Title));
            Assert.False(mine[1].IsOverdue);
            Assert.False(mine[1].HasDueDate);
        }

        [Fact]
        public async Task Overview_ShowsTheActiveSprintsLiveProgress()
        {
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Story", Status = "completed", StoryPoints = 5, SprintId = 7 },
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Story", Status = "in-progress", StoryPoints = 3, SprintId = 7 },
                new ProjectTask { Id = 3, ProjectId = 1, Type = "Story", Status = "todo", StoryPoints = 13 });
            _sprints.Query().Returns(_ => new List<Sprint> { new() { Id = 7, ProjectId = 1, Name = "Sprint 7", State = SprintStates.Active, StartDate = In(-4).Date, EndDate = In(6).Date } }.BuildMock());

            var o = await CreateSut().GetOverviewAsync(Dev, 1);

            Assert.NotNull(o.ActiveSprint);
            Assert.Equal(8, o.ActiveSprint!.TotalPoints);
            Assert.Equal(5, o.ActiveSprint.DonePoints);
            Assert.Equal((2, 1), (o.ActiveSprint.ItemsTotal, o.ActiveSprint.ItemsDone));
            Assert.InRange(o.ActiveSprint.DaysLeft, 5, 6);
        }

        [Fact]
        public async Task Overview_RecentActivity_IncludesTheProjectsItemsAndProjectEvents_Only()
        {
            SetTasks(new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Title = "Mine", Status = "todo" });
            _log.Query().Returns(_ => new List<ActivityLogEntry>
            {
                new() { EntityType = "WorkItem", EntityId = 2, EntityNameSnapshot = "Mine", ActorNameSnapshot = "Dana", Action = "commented", Timestamp = In(0) },
                new() { EntityType = "WorkItem", EntityId = 99, EntityNameSnapshot = "Elsewhere", ActorNameSnapshot = "Sam", Action = "commented", Timestamp = In(0) },
                new() { EntityType = "Project", EntityId = 1, EntityNameSnapshot = "Sam", ActorNameSnapshot = "Dana", Action = "member-removed", Timestamp = In(-1) },
                new() { EntityType = "Project", EntityId = 2, EntityNameSnapshot = "Other project", ActorNameSnapshot = "Dana", Action = "board-configured", Timestamp = In(-1) },
            }.BuildMock());

            var o = await CreateSut().GetOverviewAsync(Dev, 1);

            Assert.Equal(2, o.RecentActivity.Count);
            Assert.Equal("commented on \"Mine\"", o.RecentActivity[0].Text);
            Assert.Equal(2, o.RecentActivity[0].ItemId);
            Assert.Null(o.RecentActivity[1].ItemId);
        }

        [Fact]
        public async Task MyWork_WithoutModuleAccess_IsForbidden()
        {
            _access.HasModuleAccessAsync(default!).ReturnsForAnyArgs(false);
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().GetMyWorkAsync(Dev));
        }

        [Fact]
        public async Task MyWork_ListsOnlyMyOpenLeafWork_OverdueFirst()
        {
            var p = new Project { Id = 1, Name = "Khoi" };
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Project = p, Type = "Task", Title = "later", Status = "todo", AssignedToId = 10, DueDate = In(9) },
                new ProjectTask { Id = 2, ProjectId = 1, Project = p, Type = "Task", Title = "overdue", Status = "todo", AssignedToId = 10, DueDate = In(-3) },
                new ProjectTask { Id = 3, ProjectId = 1, Project = p, Type = "Task", Title = "soon", Status = "in-progress", AssignedToId = 10, DueDate = In(1) },
                new ProjectTask { Id = 4, ProjectId = 1, Project = p, Type = "Task", Title = "done", Status = "completed", AssignedToId = 10, DueDate = In(-3) },
                new ProjectTask { Id = 5, ProjectId = 1, Project = p, Type = "Task", Title = "theirs", Status = "todo", AssignedToId = 99, DueDate = In(-3) },
                new ProjectTask { Id = 6, ProjectId = 1, Project = p, Type = "Epic", Title = "epic", Status = "todo", AssignedToId = 10, DueDate = In(-3) });

            var mine = await CreateSut().GetMyWorkAsync(Dev);

            Assert.Equal(new[] { "overdue", "soon", "later" }, mine.Select(m => m.Title));
            Assert.True(mine[0].IsOverdue);
            Assert.All(mine, m => Assert.Equal("Khoi", m.ProjectName));
        }

        [Fact]
        public async Task ItemActivity_MergesLogAndStatusChanges_SkippingMovesAndTheCreationRow()
        {
            SetTasks(new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Title = "Mine" });
            _log.Query().Returns(_ => new List<ActivityLogEntry>
            {
                new() { EntityType = "WorkItem", EntityId = 2, EntityNameSnapshot = "Mine", ActorNameSnapshot = "Dana", Action = "created", Timestamp = In(-5) },
                new() { EntityType = "WorkItem", EntityId = 2, EntityNameSnapshot = "Mine", ActorNameSnapshot = "Sam", Action = "moved", Details = "todo -> in-progress", Timestamp = In(-3) },
                new() { EntityType = "WorkItem", EntityId = 2, EntityNameSnapshot = "Mine", ActorNameSnapshot = "Sam", Action = "commented", Timestamp = In(-1) },
            }.BuildMock());
            _history.Query().Returns(_ => new List<TaskStatusHistory>
            {
                new() { TaskId = 2, FromStatus = null, ToStatus = "todo", ChangedAt = In(-5) },
                new() { TaskId = 2, FromStatus = "todo", ToStatus = "in-progress", ChangedAt = In(-3), ChangedBy = new User { Name = "Sam" } },
            }.BuildMock());

            var feed = await CreateSut().GetItemActivityAsync(Dev, 2);

            Assert.Equal(
                new[] { "Sam: commented", "Sam: moved this from To do to In progress", "Dana: created this item" },
                feed.Select(f => $"{f.ActorName}: {f.Text}"));
            Assert.Equal("status", feed[1].Kind);
        }

        [Fact]
        public async Task ItemActivity_OnAnUnknownItem_IsNotFound()
        {
            SetTasks();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().GetItemActivityAsync(Dev, 404));
        }
    }

    public class PmLabelServiceTests
    {
        private static readonly PmCaller Dev = new(10, false);

        private readonly IRepository<Tag> _tags = Substitute.For<IRepository<Tag>>();
        private readonly IRepository<TaskTag> _taskTags = Substitute.For<IRepository<TaskTag>>();
        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly IActivityLogService _activity = Substitute.For<IActivityLogService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        public PmLabelServiceTests()
        {
            _tasks.Query().Returns(_ => new List<ProjectTask> { new() { Id = 1, ProjectId = 1, Title = "T" } }.BuildMock());
            SetTags();
            SetTaskTags();
        }

        private PmLabelService CreateSut() => new(_tags, _taskTags, _tasks, _access, _activity, _uow);
        private void SetTags(params Tag[] t) => _tags.Query().Returns(_ => t.ToList().BuildMock());
        private void SetTaskTags(params TaskTag[] t) => _taskTags.Query().Returns(_ => t.ToList().BuildMock());

        [Fact]
        public async Task SetLabels_AsViewer_IsForbidden()
        {
            _access.RequireAsync(Dev, 1, PmAction.EditItem).Returns(Task.FromException(new ForbiddenException()));
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().SetLabelsAsync(Dev, 1, new SetLabelsDto { Labels = { "ui" } }));
            _taskTags.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task SetLabels_ReusesAnExistingTagCaseInsensitively_AndCreatesNewOnes()
        {
            var existing = new Tag { Id = 4, Name = "Backend", Color = "#111111" };
            SetTags(existing);
            var added = new List<TaskTag>();
            _taskTags.When(r => r.Add(Arg.Any<TaskTag>())).Do(ci => added.Add(ci.Arg<TaskTag>()));

            var labels = await CreateSut().SetLabelsAsync(Dev, 1, new SetLabelsDto { Labels = { "  backend ", "Customer   Facing" } });

            Assert.Equal(2, added.Count);
            Assert.Same(existing, added.Single(a => a.Tag.Name == "Backend").Tag);
            var created = added.Single(a => a.Tag.Name == "Customer Facing").Tag;     // whitespace collapsed
            Assert.Equal(PmLabelService.ColorFor("Customer Facing"), created.Color);
            Assert.Equal(new[] { "Backend", "Customer Facing" }, labels.Select(l => l.Name));
        }

        [Fact]
        public async Task SetLabels_RemovesDroppedOnes_AndKeepsUnchangedWithoutReadding()
        {
            var keep = new Tag { Id = 1, Name = "ui" };
            var drop = new Tag { Id = 2, Name = "old" };
            SetTags(keep, drop);
            var keepRow = new TaskTag { TaskId = 1, TagId = 1, Tag = keep };
            var dropRow = new TaskTag { TaskId = 1, TagId = 2, Tag = drop };
            SetTaskTags(keepRow, dropRow);

            await CreateSut().SetLabelsAsync(Dev, 1, new SetLabelsDto { Labels = { "UI" } });

            _taskTags.Received(1).RemoveRange(Arg.Is<IEnumerable<TaskTag>>(e => e.SequenceEqual(new[] { dropRow })));
            _taskTags.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task SetLabels_DeletesATagThatNothingUsesAnyMore_ButNeverABuiltInOne_OrOneStillInUse()
        {
            var orphan = new Tag { Id = 2, Name = "old" };
            var builtIn = new Tag { Id = 3, Name = "Web" };
            var stillUsed = new Tag { Id = 4, Name = "shared", TaskTags = new List<TaskTag> { new() { TaskId = 99, TagId = 4 } } };
            SetTags(orphan, builtIn, stillUsed);
            SetTaskTags(
                new TaskTag { TaskId = 1, TagId = 2, Tag = orphan },
                new TaskTag { TaskId = 1, TagId = 3, Tag = builtIn },
                new TaskTag { TaskId = 1, TagId = 4, Tag = stillUsed });

            await CreateSut().SetLabelsAsync(Dev, 1, new SetLabelsDto());   // remove them all from this item

            _tags.Received(1).RemoveRange(Arg.Is<IEnumerable<Tag>>(e => e.SequenceEqual(new[] { orphan })));
        }

        [Fact]
        public async Task SetLabels_RunsOneWriteAtATime()
        {
            var inside = 0;
            var maxInside = 0;
            _uow.SaveChangesAsync().Returns(async _ =>
            {
                var now = Interlocked.Increment(ref inside);
                maxInside = Math.Max(maxInside, now);
                await Task.Delay(30);
                Interlocked.Decrement(ref inside);
                return 1;
            });

            await Task.WhenAll(Enumerable.Range(0, 4).Select(i => CreateSut().SetLabelsAsync(Dev, 1, new SetLabelsDto { Labels = { "label" + i } })));

            Assert.Equal(1, maxInside);
        }

        [Fact]
        public async Task SetLabels_DeduplicatesAndIgnoresBlanks()
        {
            var added = new List<TaskTag>();
            _taskTags.When(r => r.Add(Arg.Any<TaskTag>())).Do(ci => added.Add(ci.Arg<TaskTag>()));

            await CreateSut().SetLabelsAsync(Dev, 1, new SetLabelsDto { Labels = { "ui", "UI", "  ", "" } });

            Assert.Single(added);
        }

        [Fact]
        public async Task SetLabels_EnforcesCountAndLengthLimits()
        {
            var tooMany = Enumerable.Range(1, PmLabelService.MaxLabelsPerItem + 1).Select(i => $"l{i}").ToList();
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SetLabelsAsync(Dev, 1, new SetLabelsDto { Labels = tooMany }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SetLabelsAsync(Dev, 1, new SetLabelsDto { Labels = { new string('x', PmLabelService.MaxLabelLength + 1) } }));
        }

        [Fact]
        public void ColorFor_IsStable_AndCaseInsensitive()
        {
            Assert.Equal(PmLabelService.ColorFor("Frontend"), PmLabelService.ColorFor("frontend"));
            Assert.Matches("^#[0-9A-F]{6}$", PmLabelService.ColorFor("anything"));
        }

        [Fact]
        public async Task ProjectLabels_AreDistinctAndSorted()
        {
            var ui = new Tag { Id = 1, Name = "ui", Color = "#1" };
            var api = new Tag { Id = 2, Name = "api", Color = "#2" };
            SetTaskTags(
                new TaskTag { TaskId = 1, TagId = 1, Tag = ui, Task = new ProjectTask { ProjectId = 1 } },
                new TaskTag { TaskId = 2, TagId = 1, Tag = ui, Task = new ProjectTask { ProjectId = 1 } },
                new TaskTag { TaskId = 3, TagId = 2, Tag = api, Task = new ProjectTask { ProjectId = 1 } },
                new TaskTag { TaskId = 4, TagId = 2, Tag = api, Task = new ProjectTask { ProjectId = 2 } });

            var labels = await CreateSut().GetProjectLabelsAsync(Dev, 1);

            Assert.Equal(new[] { "api", "ui" }, labels.Select(l => l.Name));
        }
    }

    public class PmMemberImportTests
    {
        private static readonly PmCaller Admin = new(1, false);
        private static readonly PmCaller Super = new(2, true, true);

        private readonly IRepository<Project> _projects = Substitute.For<IRepository<Project>>();
        private readonly IRepository<ProjectUser> _members = Substitute.For<IRepository<ProjectUser>>();
        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
        private readonly IRepository<UserRole> _userRoles = Substitute.For<IRepository<UserRole>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly IActivityLogService _activity = Substitute.For<IActivityLogService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        private readonly User _dana = new() { Id = 10, Name = "Dana", Email = "Dana@khoitech.Africa", IsActive = true, PmAccess = true };
        private readonly User _sam = new() { Id = 11, Name = "Sam", Email = "sam@khoitech.africa", IsActive = true, PmAccess = true };
        private readonly User _noAccess = new() { Id = 12, Name = "Pat", Email = "pat@khoitech.africa", IsActive = true, PmAccess = false };
        private readonly User _inactive = new() { Id = 13, Name = "Old", Email = "old@khoitech.africa", IsActive = false, PmAccess = true };

        public PmMemberImportTests()
        {
            _users.Query().Returns(_ => new List<User> { _dana, _sam, _noAccess, _inactive }.BuildMock());
            _userRoles.Query().Returns(_ => new List<UserRole>().BuildMock());
            SetRoster(new ProjectUser { ProjectId = 1, UserId = 1, ProjectRole = ProjectRoles.ProjectAdmin });
        }

        private PmProjectService CreateSut() => new(_projects, _members, _tasks, _users, _userRoles, _access, _activity, _uow);
        private void SetRoster(params ProjectUser[] m) => _members.Query().Returns(_ => m.ToList().BuildMock());
        private static ImportMemberRowDto Row(string email, string? role = null) => new() { Email = email, Role = role };

        [Fact]
        public async Task Import_WithoutManagePermission_IsForbidden()
        {
            _access.RequireAsync(Admin, 1, PmAction.ManageMembers).Returns(Task.FromException(new ForbiddenException()));
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto { Rows = { Row("dana@khoitech.africa") } }));
        }

        [Fact]
        public async Task Import_AddsNewMembers_MatchingEmailsCaseInsensitively_WithFriendlyRoles()
        {
            var added = new List<ProjectUser>();
            _members.When(r => r.Add(Arg.Any<ProjectUser>())).Do(ci => added.Add(ci.Arg<ProjectUser>()));

            var result = await CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto
            {
                Rows = { Row("DANA@khoitech.africa", "Scrum Master"), Row("sam@khoitech.africa", "dev") },
            });

            Assert.Equal(2, result.Added);
            Assert.Equal(new[] { ProjectRoles.ScrumMaster, ProjectRoles.Developer }, added.Select(a => a.ProjectRole));
            Assert.All(result.Rows, r => Assert.Equal(ImportOutcomes.Added, r.Outcome));
            await _uow.Received(1).SaveChangesAsync();
            await _activity.Received(1).LogAsync("Project", 1, "Members", Admin.UserId, "members-imported", Arg.Is<string?>(d => d!.Contains("2 added")));
        }

        [Fact]
        public async Task Import_BlankRole_DefaultsToDeveloper()
        {
            var result = await CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto { Rows = { Row("sam@khoitech.africa", "") } });
            Assert.Equal(ProjectRoles.Developer, result.Rows[0].Role);
        }

        [Fact]
        public async Task Import_ReportsEachProblemRowWithoutBlockingTheGoodOnes()
        {
            var result = await CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto
            {
                Rows =
                {
                    Row("sam@khoitech.africa", "Tester"),     // good
                    Row("nobody@khoitech.africa"),            // not found
                    Row("old@khoitech.africa"),               // inactive
                    Row("pat@khoitech.africa"),               // no module access
                    Row("not-an-email"),                      // invalid email
                    Row("dana@khoitech.africa", "wizard"),    // invalid role
                    Row("SAM@khoitech.africa", "Viewer"),     // duplicate of row 1 (which was valid)
                },
            });

            Assert.Equal(
                new[] { ImportOutcomes.Added, ImportOutcomes.Unavailable, ImportOutcomes.Unavailable, ImportOutcomes.Unavailable, ImportOutcomes.InvalidEmail, ImportOutcomes.InvalidRole, ImportOutcomes.Duplicate },
                result.Rows.Select(r => r.Outcome));
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, result.Rows.Select(r => r.Row));
            Assert.Equal((1, 6), (result.Added, result.Skipped));
            _members.Received(1).Add(Arg.Is<ProjectUser>(m => m.UserId == 11 && m.ProjectRole == ProjectRoles.Tester));
        }

        [Fact]
        public async Task Import_ExistingMember_IsUpdatedOrLeftUnchanged()
        {
            var danaRow = new ProjectUser { ProjectId = 1, UserId = 10, ProjectRole = ProjectRoles.Developer };
            var samRow = new ProjectUser { ProjectId = 1, UserId = 11, ProjectRole = ProjectRoles.Tester };
            SetRoster(new ProjectUser { ProjectId = 1, UserId = 1, ProjectRole = ProjectRoles.ProjectAdmin }, danaRow, samRow);

            var result = await CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto
            {
                Rows = { Row("dana@khoitech.africa", "Scrum Master"), Row("sam@khoitech.africa", "Tester") },
            });

            Assert.Equal(ProjectRoles.ScrumMaster, danaRow.ProjectRole);
            Assert.Equal(ProjectRoles.Tester, samRow.ProjectRole);
            Assert.Equal(new[] { ImportOutcomes.Updated, ImportOutcomes.Unchanged }, result.Rows.Select(r => r.Outcome));
            _members.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task Import_ThatWouldRemoveTheLastProjectAdmin_ChangesNothing()
        {
            var adminRow = new ProjectUser { ProjectId = 1, UserId = 10, ProjectRole = ProjectRoles.ProjectAdmin };
            SetRoster(adminRow);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto
            {
                Rows = { Row("dana@khoitech.africa", "Developer") },
            }));

            Assert.Contains("Project Admin", ex.Message);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task Import_DryRun_ReportsButChangesNothing()
        {
            var danaRow = new ProjectUser { ProjectId = 1, UserId = 10, ProjectRole = ProjectRoles.Developer };
            SetRoster(new ProjectUser { ProjectId = 1, UserId = 1, ProjectRole = ProjectRoles.ProjectAdmin }, danaRow);

            var result = await CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto
            {
                DryRun = true, Rows = { Row("dana@khoitech.africa", "Scrum Master"), Row("sam@khoitech.africa") },
            });

            Assert.True(result.DryRun);
            Assert.Equal((1, 1), (result.Added, result.Updated));
            Assert.Equal(ProjectRoles.Developer, danaRow.ProjectRole);
            _members.DidNotReceiveWithAnyArgs().Add(default!);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task Import_GrantingModuleAccess_NeedsAPmAdmin()
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto
            {
                GrantModuleAccess = true, Rows = { Row("pat@khoitech.africa") },
            }));
            Assert.False(_noAccess.PmAccess);
        }

        [Fact]
        public async Task Import_ByASuperAdmin_CanGrantAccess_AndConfineToTheModule()
        {
            var result = await CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                GrantModuleAccess = true, ModuleOnly = true, Rows = { Row("pat@khoitech.africa", "Tester") },
            });

            Assert.True(_noAccess.PmAccess);
            Assert.True(_noAccess.PmOnly);
            Assert.Equal(1, result.ModuleAccessGranted);
            Assert.Contains("Module access granted", result.Rows[0].Message);
        }

        [Fact]
        public async Task Import_ByAProjectAdmin_DoesNotRevealWhyAnEmailCannotBeAdded()
        {
            var result = await CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto
            {
                Rows = { Row("nobody@khoitech.africa"), Row("old@khoitech.africa"), Row("pat@khoitech.africa") },
            });

            Assert.All(result.Rows, r => Assert.Equal(ImportOutcomes.Unavailable, r.Outcome));
            Assert.Equal(1, result.Rows.Select(r => r.Message).Distinct().Count());   // identical wording for all three
        }

        [Fact]
        public async Task Import_ByAPmAdmin_SeesTheSpecificReasons()
        {
            var result = await CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                Rows = { Row("nobody@khoitech.africa"), Row("old@khoitech.africa"), Row("pat@khoitech.africa") },
            });

            Assert.Equal(new[] { ImportOutcomes.NotFound, ImportOutcomes.Inactive, ImportOutcomes.NoModuleAccess }, result.Rows.Select(r => r.Outcome));
        }

        [Fact]
        public async Task Import_AFailedRowDoesNotMakeALaterCorrectRowForTheSamePersonADuplicate()
        {
            var result = await CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto
            {
                Rows = { Row("sam@khoitech.africa", "wizard"), Row("sam@khoitech.africa", "Tester") },
            });

            Assert.Equal(new[] { ImportOutcomes.InvalidRole, ImportOutcomes.Added }, result.Rows.Select(r => r.Outcome));
        }

        [Fact]
        public async Task Import_ModuleOnly_IsNotAppliedToSomeoneWhoHasOtherPermissions()
        {
            var role = new Role { Id = 5, Name = "Finance", RolePermissions = new List<RolePermission> { new(), new() } };
            _userRoles.Query().Returns(_ => new List<UserRole> { new() { UserId = _noAccess.Id, RoleId = 5, Role = role } }.BuildMock());

            var result = await CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                GrantModuleAccess = true, ModuleOnly = true, Rows = { Row("pat@khoitech.africa") },
            });

            Assert.True(_noAccess.PmAccess);
            Assert.False(_noAccess.PmOnly);
            Assert.False(result.Rows[0].ConfinedToModule);
            Assert.Contains("other permissions", result.Rows[0].Message);
        }

        [Fact]
        public async Task Import_GrantOnlyForSomeoneAlreadyOnTheProject_IsNotReportedAsARoleChange()
        {
            _noAccess.PmAccess = false;
            SetRoster(new ProjectUser { ProjectId = 1, UserId = 1, ProjectRole = ProjectRoles.ProjectAdmin },
                      new ProjectUser { ProjectId = 1, UserId = 12, ProjectRole = ProjectRoles.Developer });

            var result = await CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                GrantModuleAccess = true, Rows = { Row("pat@khoitech.africa", "Developer") },
            });

            Assert.Equal(ImportOutcomes.AccessGranted, result.Rows[0].Outcome);
            Assert.Equal((0, 0, 1), (result.Added, result.Updated, result.ModuleAccessGranted));
            Assert.DoesNotContain("Role changed", result.Rows[0].Message);
        }

        [Fact]
        public async Task Import_ModuleOnlyWithoutGrant_IsRejected() =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                ModuleOnly = true, Rows = { Row("sam@khoitech.africa") },
            }));

        [Fact]
        public async Task Import_RejectsEmptyAndOversizedLists()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ImportMembersAsync(Admin, 1, new ImportMembersDto()));
            var big = new ImportMembersDto { Rows = Enumerable.Range(0, PmProjectService.MaxImportRows + 1).Select(i => Row($"u{i}@x.com")).ToList() };
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ImportMembersAsync(Admin, 1, big));
        }
    }

    public class PmBacklogRollupTests
    {
        private static readonly PmCaller Dev = new(10, false);

        private static PmWorkItemService Sut(IRepository<ProjectTask> tasks, IProjectAccessService? access = null) => new(tasks,
            Substitute.For<IRepository<Project>>(), Substitute.For<IRepository<ProjectUser>>(), Substitute.For<IRepository<TaskStatusHistory>>(),
            Substitute.For<IRepository<Attachment>>(), Substitute.For<IFileStorage>(), Substitute.For<IPmWorkflowService>(),
            access ?? Substitute.For<IProjectAccessService>(), Substitute.For<IActivityLogService>(), Substitute.For<IUnitOfWork>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PmWorkItemService>.Instance);

        [Fact]
        public async Task Lookup_ReturnsOnlyTheFieldsAPickerNeeds_InRankOrder_ForTheProject()
        {
            var tasks = Substitute.For<IRepository<ProjectTask>>();
            tasks.Query().Returns(_ => new List<ProjectTask>
            {
                new() { Id = 2, ProjectId = 1, Title = "B", Type = "Task", Status = "todo", Rank = 2000, Description = "long text" },
                new() { Id = 1, ProjectId = 1, Title = "A", Type = "Epic", Status = "todo", Rank = 1000 },
                new() { Id = 3, ProjectId = 9, Title = "Elsewhere", Type = "Task", Status = "todo", Rank = 1 },
            }.BuildMock());

            var lookup = await Sut(tasks).GetLookupAsync(Dev, 1);

            Assert.Equal(new[] { "A", "B" }, lookup.Select(l => l.Title));
            Assert.Equal("Epic", lookup[0].Type);
        }

        [Fact]
        public async Task Backlog_CanSkipTheRollupQuery()
        {
            var tasks = Substitute.For<IRepository<ProjectTask>>();
            var epic = new ProjectTask { Id = 1, ProjectId = 1, Type = "Epic", Title = "E", Status = "todo" };
            var child = new ProjectTask { Id = 2, ProjectId = 1, Type = "Story", Title = "S", Status = "completed", ParentId = 1, Parent = epic };
            epic.Children = new List<ProjectTask> { child };
            var calls = 0;
            tasks.Query().Returns(_ => { calls++; return new List<ProjectTask> { epic, child }.BuildMock(); });

            await Sut(tasks).GetBacklogAsync(Dev, 1, new WorkItemFilterDto(), includeRollup: false);
            var withoutRollup = calls;
            calls = 0;
            await Sut(tasks).GetBacklogAsync(Dev, 1, new WorkItemFilterDto());

            Assert.Equal(1, withoutRollup);
            Assert.Equal(2, calls);   // the list query plus the project-wide roll-up query
        }

        [Fact]
        public async Task Backlog_FillsInRollupForContainers_FromTheWholeProject_AndFiltersByLabel()
        {
            var tasks = Substitute.For<IRepository<ProjectTask>>();
            var epic = new ProjectTask { Id = 1, ProjectId = 1, Type = "Epic", Title = "E", Status = "todo" };
            var story = new ProjectTask { Id = 2, ProjectId = 1, Type = "Story", Title = "S", Status = "completed", StoryPoints = 5, ParentId = 1, Parent = epic };
            var story2 = new ProjectTask { Id = 3, ProjectId = 1, Type = "Story", Title = "S2", Status = "todo", StoryPoints = 3, ParentId = 1, Parent = epic };
            epic.Children = new List<ProjectTask> { story, story2 };
            story.TaskTags = new List<TaskTag> { new() { TaskId = 2, TagId = 9, Tag = new Tag { Id = 9, Name = "ui", Color = "#1" } } };
            tasks.Query().Returns(_ => new List<ProjectTask> { epic, story, story2 }.BuildMock());
            var access = Substitute.For<IProjectAccessService>();
            var sut = new PmWorkItemService(tasks, Substitute.For<IRepository<Project>>(), Substitute.For<IRepository<ProjectUser>>(),
                Substitute.For<IRepository<TaskStatusHistory>>(), Substitute.For<IRepository<Attachment>>(), Substitute.For<IFileStorage>(),
                Substitute.For<IPmWorkflowService>(), access, Substitute.For<IActivityLogService>(), Substitute.For<IUnitOfWork>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PmWorkItemService>.Instance);

            var all = await sut.GetBacklogAsync(Dev, 1, new WorkItemFilterDto());
            var epicDto = all.Single(i => i.Id == 1);
            Assert.Equal((1, 2, 5, 8), (epicDto.RollupDone, epicDto.RollupTotal, epicDto.RollupPointsDone, epicDto.RollupPointsTotal));
            Assert.Equal("ui", Assert.Single(all.Single(i => i.Id == 2).Labels).Name);

            var labelled = await sut.GetBacklogAsync(Dev, 1, new WorkItemFilterDto { LabelId = 9 });
            Assert.Equal(new[] { 2 }, labelled.Select(i => i.Id));
        }
    }
}
