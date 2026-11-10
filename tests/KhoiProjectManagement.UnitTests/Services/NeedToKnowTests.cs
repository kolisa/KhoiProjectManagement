using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // The reported leak: an ordinary signed-in user could list, open and create tickets in projects they were never part of.
    // Every classic endpoint now goes through IProjectScope. These tests call each service as an ORDINARY MEMBER of project 1
    // (Developer), a Viewer of project 2 and with no link at all to project 5, using the real scope - and assert that
    // project 5 simply does not exist for them, and that what they may change follows their role.
    public class NeedToKnowTests
    {
        private const int Me = ScopeFixture.Me;

        private static ProjectScope Member(bool readOnly = false) => ScopeFixture.For(Me,
            memberships: new[] { (1, ProjectRoles.Developer), (2, ProjectRoles.Viewer) }, readOnly: readOnly);

        private static ProjectTask Task(int id, int projectId, string title = "T") =>
            new() { Id = id, ProjectId = projectId, Title = title, Status = "todo", Priority = "medium", Project = new Project { Id = projectId, Name = $"Project {projectId}" } };

        // IRepository<T>.FindAsync(params object[]) returns a ValueTask: serve the given entities by Id.
        private static void ServeById<T>(IRepository<T> repo, params T[] items) where T : BaseEntity =>
            repo.FindAsync(Arg.Any<object[]>()).Returns(ci => new ValueTask<T?>(items.FirstOrDefault(i => i.Id == (int)((object[])ci[0])[0])));

        // ---------------------------------------------------------------- tasks
        public class Tasks
        {
            private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
            private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
            private readonly IRepository<TaskStatusHistory> _history = Substitute.For<IRepository<TaskStatusHistory>>();

            public Tasks()
            {
                _tasks.Query().Returns(_ => new List<ProjectTask> { Task(1, 1, "mine"), Task(2, 2, "viewer-only"), Task(3, 5, "someone else's") }.BuildMock());
                ServeById(_tasks, Task(1, 1), Task(2, 2), Task(3, 5));
            }

            private TaskService Sut(ProjectScope scope) => new(
                _tasks, Substitute.For<IRepository<User>>(), Substitute.For<IRepository<Project>>(), Substitute.For<IRepository<TaskTag>>(),
                Substitute.For<IRepository<Tag>>(), _uow, Substitute.For<INotificationService>(), Substitute.For<IEmailService>(),
                Substitute.For<IActivityLogService>(), _history, Substitute.For<IPmWorkflowService>(), scope);

            [Fact]
            public async Task TheListContainsOnlyTasksFromTheCallersProjects()
            {
                var list = await Sut(Member()).GetTasksAsync(new TaskFilterDto());

                Assert.Equal(new[] { "mine", "viewer-only" }, list.Select(t => t.Title).OrderBy(t => t));
            }

            [Fact]
            public async Task FilteringByAProjectTheyAreNotOn_ReturnsNothing_NotThatProjectsTasks()
            {
                var list = await Sut(Member()).GetTasksAsync(new TaskFilterDto { ProjectId = 5 });
                Assert.Empty(list);
            }

            [Fact]
            public async Task AnAdministrator_StillSeesEverything()
            {
                var list = await Sut(ScopeFixture.For(Me, administrator: true)).GetTasksAsync(new TaskFilterDto());
                Assert.Equal(3, list.Count());
            }

            [Fact]
            public async Task OpeningSomeoneElsesTaskById_FindsNothing()
            {
                var sut = Sut(Member());
                Assert.NotNull(await sut.GetTaskByIdAsync(1));
                Assert.Null(await sut.GetTaskByIdAsync(3));
            }

            [Fact]
            public async Task CreatingATicketInAProjectTheyAreNotOn_IsRefused_AndNothingIsWritten()
            {
                await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut(Member()).CreateTaskAsync(
                    new CreateTaskDto { ProjectId = 5, Title = "sneaky", Priority = "low", DueDate = DateTime.UtcNow.AddDays(3) }));

                _tasks.DidNotReceiveWithAnyArgs().Add(default!);
                await _uow.DidNotReceive().SaveChangesAsync();
            }

            [Fact]
            public async Task AViewerCannotCreateATicketEvenInTheirOwnProject()
            {
                await Assert.ThrowsAsync<ForbiddenException>(() => Sut(Member()).CreateTaskAsync(
                    new CreateTaskDto { ProjectId = 2, Title = "no", Priority = "low", DueDate = DateTime.UtcNow.AddDays(3) }));
                _tasks.DidNotReceiveWithAnyArgs().Add(default!);
            }

            [Fact]
            public async Task ReadOnlyAccessCannotCreateATicketEitherWayItIsAsked()
            {
                await Assert.ThrowsAsync<ForbiddenException>(() => Sut(Member(readOnly: true)).CreateTaskAsync(
                    new CreateTaskDto { ProjectId = 1, Title = "no", Priority = "low", DueDate = DateTime.UtcNow.AddDays(3) }));
            }

            [Fact]
            public async Task ChangingSomeoneElsesTask_ReportsNotFound_AndChangesNothing()
            {
                var sut = Sut(Member());

                Assert.False(await sut.UpdateTaskStatusAsync(3, "completed", Me));
                Assert.False(await sut.DeleteTaskAsync(3));
                Assert.False(await sut.UpdateTaskAsync(3, new UpdateTaskDto { Title = "x", Status = "todo", Priority = "low", Type = "Task", DueDate = DateTime.UtcNow }, Me));

                _tasks.DidNotReceiveWithAnyArgs().Remove(default!);
                await _uow.DidNotReceive().SaveChangesAsync();
            }

            [Fact]
            public async Task AViewerCannotMoveOrDeleteATask()
            {
                var sut = Sut(Member());
                await Assert.ThrowsAsync<ForbiddenException>(() => sut.UpdateTaskStatusAsync(2, "completed", Me));
                await Assert.ThrowsAsync<ForbiddenException>(() => sut.DeleteTaskAsync(2));
                _tasks.DidNotReceiveWithAnyArgs().Remove(default!);
            }

            [Fact]
            public async Task ADeveloperCanStillWorkInTheirOwnProject()
            {
                var sut = Sut(Member());
                Assert.True(await sut.UpdateTaskStatusAsync(1, "in-progress", Me));
                Assert.True(await sut.DeleteTaskAsync(1));
            }

            [Fact]
            public async Task TheOverdueListIsScopedToo()
            {
                _tasks.Query().Returns(_ => new List<ProjectTask>
                {
                    new() { Id = 1, ProjectId = 1, Status = "todo", HasExplicitDueDate = true, DueDate = DateTime.UtcNow.AddDays(-2), Project = new Project { Name = "mine" } },
                    new() { Id = 2, ProjectId = 5, Status = "todo", HasExplicitDueDate = true, DueDate = DateTime.UtcNow.AddDays(-2), Project = new Project { Name = "theirs" } },
                }.BuildMock());

                var overdue = await Sut(Member()).GetOverdueTasksAsync();

                Assert.Equal(new[] { 1 }, overdue.Select(t => t.Id));
            }
        }

        // ---------------------------------------------------------------- projects
        public class Projects
        {
            private readonly IRepository<Project> _projects = Substitute.For<IRepository<Project>>();
            private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

            public Projects()
            {
                var creator = new User { Id = 1, Name = "Boss" };
                _projects.Query().Returns(_ => new List<Project>
                {
                    new() { Id = 1, Name = "Mine", Creator = creator, CreatedBy = 1 },
                    new() { Id = 2, Name = "Viewer on this", Creator = creator, CreatedBy = 1 },
                    new() { Id = 5, Name = "Secret", Creator = creator, CreatedBy = 1 },
                }.BuildMock());
                ServeById(_projects, new Project { Id = 1 }, new Project { Id = 2 }, new Project { Id = 5 });
                _uow.BeginTransactionAsync().Returns(Substitute.For<IAppTransaction>());
            }

            private ProjectService Sut(ProjectScope scope) => new(
                _projects, Substitute.For<IRepository<ProjectUser>>(), Substitute.For<IRepository<User>>(), Substitute.For<IRepository<ProjectTag>>(),
                Substitute.For<IRepository<Tag>>(), _uow, Substitute.For<INotificationService>(), Substitute.For<ISpaceService>(),
                Substitute.For<IEmailService>(), Substitute.For<IActivityLogService>(), scope);

            [Fact]
            public async Task TheProjectListShowsOnlyProjectsTheCallerBelongsTo()
            {
                var list = await Sut(Member()).GetAllProjectsAsync();
                Assert.Equal(new[] { "Mine", "Viewer on this" }, list.Select(p => p.Name).OrderBy(n => n));
            }

            [Fact]
            public async Task OpeningAProjectThatIsNotTheirs_FindsNothing()
            {
                var sut = Sut(Member());
                Assert.NotNull(await sut.GetProjectByIdAsync(1));
                Assert.Null(await sut.GetProjectByIdAsync(5));
                Assert.Null(await sut.GetProjectStatisticsAsync(5));
            }

            [Fact]
            public async Task ChangingOrDeletingAProjectThatIsNotTheirs_ReportsNotFound()
            {
                var sut = Sut(Member());
                Assert.False(await sut.UpdateProjectAsync(5, new UpdateProjectDto { Name = "x", Priority = "low", Status = "active" }));
                Assert.False(await sut.DeleteProjectAsync(5));
                _projects.DidNotReceiveWithAnyArgs().Remove(default!);
            }

            [Fact]
            public async Task AViewerCannotChangeOrDeleteTheProject()
            {
                var sut = Sut(Member());
                await Assert.ThrowsAsync<ForbiddenException>(() => sut.DeleteProjectAsync(2));
                _projects.DidNotReceiveWithAnyArgs().Remove(default!);
            }

            [Fact]
            public async Task AnAdministratorSeesEveryProject()
            {
                Assert.Equal(3, (await Sut(ScopeFixture.For(Me, administrator: true)).GetAllProjectsAsync()).Count());
            }
        }

        // ---------------------------------------------------------------- search
        public class Search
        {
            [Fact]
            public async Task SearchNeverFindsProjectsOrTasksOutsideTheCallersProjects()
            {
                var projects = Substitute.For<IRepository<Project>>();
                projects.Query().Returns(_ => new List<Project> { new() { Id = 1, Name = "Acme launch" }, new() { Id = 5, Name = "Acme takeover (secret)" } }.BuildMock());
                var tasks = Substitute.For<IRepository<ProjectTask>>();
                tasks.Query().Returns(_ => new List<ProjectTask> { new() { Id = 1, ProjectId = 1, Title = "Acme task" }, new() { Id = 2, ProjectId = 5, Title = "Acme layoffs" } }.BuildMock());
                var users = Substitute.For<IRepository<User>>();
                users.Query().Returns(_ => new List<User>().BuildMock());

                var result = await new GlobalSearchService(projects, tasks, users, Member()).SearchAsync("acme");

                Assert.Equal(new[] { "Acme launch" }, result.Projects.Select(p => p.Title));
                Assert.Equal(new[] { "Acme task" }, result.Tasks.Select(t => t.Title));
            }
        }

        // ---------------------------------------------------------------- attachments
        public class Attachments
        {
            private readonly IRepository<Attachment> _attachments = Substitute.For<IRepository<Attachment>>();
            private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
            private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

            public Attachments()
            {
                var all = new List<Attachment>
                {
                    new() { Id = 1, TaskId = 10, FileName = "mine.pdf", FilePath = "a", UploadedByUser = new User { Name = "Dana" } },
                    new() { Id = 2, TaskId = 30, FileName = "secret.pdf", FilePath = "b", UploadedByUser = new User { Name = "Dana" } },
                    new() { Id = 3, ProjectId = 5, FileName = "plan.pdf", FilePath = "c", UploadedByUser = new User { Name = "Dana" } },
                };
                _attachments.Query().Returns(_ => all.BuildMock());
                ServeById(_attachments, all.ToArray());
                _tasks.Query().Returns(_ => new List<ProjectTask> { new() { Id = 10, ProjectId = 1 }, new() { Id = 30, ProjectId = 5 } }.BuildMock());
            }

            private AttachmentService Sut(ProjectScope scope) => new(_attachments, _uow, new ConfigurationBuilder().Build(), _tasks, scope);

            [Fact]
            public async Task AFileInSomeoneElsesProject_CannotBeOpenedOrDownloaded()
            {
                var sut = Sut(Member());

                Assert.NotNull(await sut.GetAttachmentByIdAsync(1));
                Assert.Null(await sut.GetAttachmentByIdAsync(2));     // through its task
                Assert.Null(await sut.GetAttachmentByIdAsync(3));     // directly on the project
                Assert.Null(await sut.DownloadFileAsync(2));
                Assert.Null(await sut.DownloadFileAsync(3));
            }

            [Fact]
            public async Task ListingFilesOfAProjectOrTaskTheyAreNotOn_ReturnsNothing()
            {
                var sut = Sut(Member());
                Assert.Empty(await sut.GetProjectAttachmentsAsync(5));
                Assert.Empty(await sut.GetTaskAttachmentsAsync(30));
                Assert.Single(await sut.GetTaskAttachmentsAsync(10));
            }

            [Fact]
            public async Task DeletingAFileInSomeoneElsesProject_ReportsNotFound_AndRemovesNothing()
            {
                Assert.False(await Sut(Member()).DeleteAttachmentAsync(2));
                _attachments.DidNotReceiveWithAnyArgs().Remove(default!);
            }

            [Fact]
            public async Task UploadingToATaskOrProjectTheyAreNotOn_IsRefused_BeforeAnythingIsStored()
            {
                var file = Substitute.For<IFormFile>();
                file.FileName.Returns("x.pdf");
                var sut = Sut(Member());

                await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UploadFileAsync(file, Me, projectId: null, taskId: 30));
                await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UploadFileAsync(file, Me, projectId: 5, taskId: null));
                _attachments.DidNotReceiveWithAnyArgs().Add(default!);
                await file.DidNotReceiveWithAnyArgs().CopyToAsync(default!, default);
            }

            [Fact]
            public async Task AViewerCannotUploadToTheirOwnProject()
            {
                var file = Substitute.For<IFormFile>();
                file.FileName.Returns("x.pdf");
                await Assert.ThrowsAsync<ForbiddenException>(() => Sut(Member()).UploadFileAsync(file, Me, projectId: 2, taskId: null));
            }
        }

        // ---------------------------------------------------------------- dashboard + reports
        public class Dashboards
        {
            [Fact]
            public async Task TheDashboardCountsOnlyTheCallersProjects_AndDropsTheCompanyWideDeltas()
            {
                var stats = Substitute.For<IDashboardStatsRepository>();
                stats.GetCountsAsync(Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<int>?>()).Returns(new DashboardCountsResult { TotalProjects = 2, TotalTasks = 10 });
                var snapshots = Substitute.For<IRepository<DashboardStatsSnapshot>>();
                snapshots.Query().Returns(_ => new List<DashboardStatsSnapshot> { new() { CapturedAt = DateTime.UtcNow.AddDays(-9), ActiveProjects = 40, TotalTasks = 900 } }.BuildMock());
                var sut = new DashboardService(stats, Substitute.For<IRepository<ProjectTask>>(), snapshots, Substitute.For<IUnitOfWork>(), Member());

                var result = await sut.GetDashboardStatisticsAsync();

                await stats.Received(1).GetCountsAsync(Arg.Any<DateTime>(), Arg.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.OrderBy(i => i).SequenceEqual(new[] { 1, 2 })));
                Assert.Null(result.TotalTasksDelta);         // a company-wide baseline means nothing against two projects' counts
            }

            [Fact]
            public async Task AnAdministratorGetsCompanyWideCounts_AndTheDeltas()
            {
                var stats = Substitute.For<IDashboardStatsRepository>();
                stats.GetCountsAsync(Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<int>?>()).Returns(new DashboardCountsResult { TotalTasks = 1000 });
                var snapshots = Substitute.For<IRepository<DashboardStatsSnapshot>>();
                snapshots.Query().Returns(_ => new List<DashboardStatsSnapshot> { new() { CapturedAt = DateTime.UtcNow.AddDays(-9), TotalTasks = 900 } }.BuildMock());
                var sut = new DashboardService(stats, Substitute.For<IRepository<ProjectTask>>(), snapshots, Substitute.For<IUnitOfWork>(), ScopeFixture.For(Me, administrator: true));

                var result = await sut.GetDashboardStatisticsAsync();

                await stats.Received(1).GetCountsAsync(Arg.Any<DateTime>(), Arg.Is<IReadOnlyCollection<int>?>(ids => ids == null));
                Assert.Equal(100, result.TotalTasksDelta);
            }

            [Fact]
            public async Task TheWeeklyCompletionChartCountsOnlyTheCallersTasks()
            {
                var tasks = Substitute.For<IRepository<ProjectTask>>();
                var monday = DateTime.UtcNow.Date.AddDays(-(((int)DateTime.UtcNow.DayOfWeek + 6) % 7));
                tasks.Query().Returns(_ => new List<ProjectTask>
                {
                    new() { ProjectId = 1, CompletedAt = monday.AddHours(3) },
                    new() { ProjectId = 5, CompletedAt = monday.AddHours(4) },
                    new() { ProjectId = 5, CompletedAt = monday.AddHours(5) },
                }.BuildMock());
                var sut = new DashboardService(Substitute.For<IDashboardStatsRepository>(), tasks, Substitute.For<IRepository<DashboardStatsSnapshot>>(), Substitute.For<IUnitOfWork>(), Member());

                var week = await sut.GetWeeklyCompletionAsync();

                Assert.Equal(1, week.Sum());
            }

            [Fact]
            public async Task ReportsCoverOnlyTheCallersProjects()
            {
                var stats = Substitute.For<IReportStatsRepository>();
                stats.GetProjectTaskCountsAsync(Arg.Any<IReadOnlyCollection<int>?>()).Returns(new List<ProjectTaskCountsResult>());
                stats.GetTeamMemberTaskCountsAsync(Arg.Any<DateTime>(), Arg.Any<IReadOnlyCollection<int>?>()).Returns(new List<TeamMemberTaskCountsResult>());
                var tasks = Substitute.For<IRepository<ProjectTask>>();
                tasks.Query().Returns(_ => new List<ProjectTask>
                {
                    new() { Id = 1, ProjectId = 1, Status = "todo", HasExplicitDueDate = true, DueDate = DateTime.UtcNow.AddDays(-3), Project = new Project { Name = "Mine" }, Title = "m" },
                    new() { Id = 2, ProjectId = 5, Status = "todo", HasExplicitDueDate = true, DueDate = DateTime.UtcNow.AddDays(-3), Project = new Project { Name = "Secret" }, Title = "s" },
                }.BuildMock());
                var sut = new ReportService(stats, tasks, Member());

                await sut.GenerateProjectSummaryReportAsync();
                await sut.GenerateTeamPerformanceReportAsync();
                var overdue = await sut.GenerateOverdueTasksReportAsync();

                await stats.Received(1).GetProjectTaskCountsAsync(Arg.Is<IReadOnlyCollection<int>?>(ids => ids != null && ids.Contains(1) && !ids.Contains(5)));
                await stats.Received(1).GetTeamMemberTaskCountsAsync(Arg.Any<DateTime>(), Arg.Is<IReadOnlyCollection<int>?>(ids => ids != null && !ids.Contains(5)));
                Assert.Equal(new[] { "m" }, overdue.Tasks.Select(t => t.Title));
            }
        }

        // ---------------------------------------------------------------- the activity feed
        public class ActivityFeed
        {
            [Fact]
            public async Task TheFeedNamesOnlyThingsInTheCallersProjects()
            {
                var log = Substitute.For<IRepository<ActivityLogEntry>>();
                ActivityLogEntry E(int id, string type, int? entityId, string name) => new() { Id = id, EntityType = type, EntityId = entityId, EntityNameSnapshot = name, ActorNameSnapshot = "X", Action = "updated", Timestamp = DateTime.UtcNow.AddMinutes(-id) };
                log.Query().Returns(_ => new List<ActivityLogEntry>
                {
                    E(1, "WorkItem", 10, "my work item"),
                    E(2, "WorkItem", 30, "secret work item"),
                    E(3, "Task", 10, "my classic task"),
                    E(4, "Task", 30, "secret classic task"),
                    E(5, "Project", 1, "my project"),
                    E(6, "Project", 5, "secret project"),
                    E(7, "Sprint", 3, "a sprint"),
                    E(8, "User", 4, "someone's module access"),
                    E(9, "WorkItem", null, "a deleted item"),
                    E(10, "WikiPage", 7, "a wiki page"),
                }.BuildMock());
                var tasks = Substitute.For<IRepository<ProjectTask>>();
                tasks.Query().Returns(_ => new List<ProjectTask> { new() { Id = 10, ProjectId = 1 }, new() { Id = 30, ProjectId = 5 } }.BuildMock());
                var sut = new ActivityLogService(log, Substitute.For<IRepository<User>>(), Substitute.For<IUnitOfWork>(), Member(), tasks);

                var feed = await sut.GetRecentAsync(20);

                Assert.Equal(new[] { "my work item", "my classic task", "my project", "a wiki page" }, feed.Select(f => f.EntityNameSnapshot));
            }

            [Fact]
            public async Task AnAdministratorSeesTheWholeFeed()
            {
                var log = Substitute.For<IRepository<ActivityLogEntry>>();
                log.Query().Returns(_ => new List<ActivityLogEntry>
                {
                    new() { EntityType = "WorkItem", EntityId = 30, EntityNameSnapshot = "secret", ActorNameSnapshot = "X", Action = "a", Timestamp = DateTime.UtcNow },
                }.BuildMock());
                var sut = new ActivityLogService(log, Substitute.For<IRepository<User>>(), Substitute.For<IUnitOfWork>(), ScopeFixture.For(Me, administrator: true), Substitute.For<IRepository<ProjectTask>>());

                Assert.Single(await sut.GetRecentAsync(20));
            }
        }

        // ---------------------------------------------------------------- timesheets
        public class Timesheets
        {
            [Fact]
            public async Task TimeCannotBeLoggedAgainstAProjectTheCallerIsNotOn()
            {
                var timesheets = Substitute.For<IRepository<Timesheet>>();
                var sut = new TimesheetService(timesheets, Substitute.For<IRepository<TimesheetEntry>>(), Substitute.For<IRepository<User>>(),
                    Substitute.For<IRepository<UserRole>>(), Substitute.For<IRepository<RolePermission>>(), Substitute.For<INotificationService>(),
                    Substitute.For<IEmailService>(), Substitute.For<IUnitOfWork>(), Member());
                var caller = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Me.ToString()) }));

                var dto = new CreateTimesheetDto
                {
                    PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddDays(6),
                    Entries = new List<CreateTimesheetEntryDto> { new() { EntryDate = DateTime.UtcNow.Date, ProjectId = 5, Hours = 2, Description = "secret work" } },
                };

                await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateTimesheetAsync(dto, caller));
                timesheets.DidNotReceiveWithAnyArgs().Add(default!);
            }
        }

        // ---------------------------------------------------------------- Project Management's people list
        public class PeopleList
        {
            private readonly IRepository<ProjectUser> _members = Substitute.For<IRepository<ProjectUser>>();
            private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();

            private PmProjectService Sut()
            {
                var access = Substitute.For<IProjectAccessService>();
                access.HasModuleAccessAsync(default!).ReturnsForAnyArgs(true);
                _users.Query().Returns(_ => new List<User> { new() { Id = 1, Name = "Ada", Email = "a@x.com", IsActive = true, PmAccess = true } }.BuildMock());
                return new PmProjectService(Substitute.For<IRepository<Project>>(), _members, Substitute.For<IRepository<ProjectTask>>(), _users,
                    Substitute.For<IRepository<UserRole>>(), access, Substitute.For<IActivityLogService>(), Substitute.For<IUnitOfWork>());
            }

            private void MemberOf(string role) =>
                _members.Query().Returns(_ => new List<ProjectUser> { new() { ProjectId = 1, UserId = Me, ProjectRole = role } }.BuildMock());

            [Theory]
            [InlineData(ProjectRoles.Developer)]
            [InlineData(ProjectRoles.Tester)]
            [InlineData(ProjectRoles.Viewer)]
            [InlineData(ProjectRoles.ScrumMaster)]
            [InlineData(ProjectRoles.ProjectManager)]
            public async Task APersonWhoCannotManageMembers_GetsNoPeopleList(string role)
            {
                MemberOf(role);
                await Assert.ThrowsAsync<ForbiddenException>(() => Sut().GetAssignableUsersAsync(new PmCaller(Me, false)));
            }

            [Fact]
            public async Task AProjectAdmin_CanListPeopleToAdd()
            {
                MemberOf(ProjectRoles.ProjectAdmin);
                Assert.Single(await Sut().GetAssignableUsersAsync(new PmCaller(Me, false)));
            }

            [Fact]
            public async Task AnAdministrator_CanListPeopleToAdd()
            {
                MemberOf(ProjectRoles.Viewer);
                Assert.Single(await Sut().GetAssignableUsersAsync(new PmCaller(Me, true, true)));
            }
        }
    }
}
