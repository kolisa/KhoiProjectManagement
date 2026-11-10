using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Follow-ups from the need-to-know code review: edge cases around the same scope rule.
    public class NeedToKnowFollowUpTests
    {
        private static ProjectScope Member() => ScopeFixture.For(ScopeFixture.Me, memberships: new[] { (1, ProjectRoles.Developer) });

        [Fact]
        public async Task ATaskAssignedToTheCaller_StaysVisibleToThem_EvenOnAProjectTheyAreNotOn()
        {
            var scope = Member();
            var tasks = new List<ProjectTask>
            {
                new() { Id = 1, ProjectId = 1 },
                new() { Id = 2, ProjectId = 5, AssignedToId = ScopeFixture.Me },   // assigned to me, project not mine
                new() { Id = 3, ProjectId = 5, AssignedToId = 99 },                // someone else's, project not mine
            }.BuildMock();

            Assert.Equal(new[] { 1, 2 }, (await scope.ScopeAsync(tasks)).Select(t => t.Id));
        }

        [Fact]
        public async Task AssignmentGivesNoRightToChangeAnythingInThatProject()
        {
            Assert.Equal(ProjectAccessOutcome.NotVisible, await Member().CheckAsync(5, ProjectAbility.Edit));
        }

        [Fact]
        public async Task TheActivityFeed_IsNotStarvedByBusierProjects()
        {
            // 300 newer entries about someone else's project, then one older entry about the caller's own.
            var entries = Enumerable.Range(1, 300)
                .Select(i => new ActivityLogEntry { Id = i, EntityType = "Project", EntityId = 5, EntityNameSnapshot = "busy secret", ActorNameSnapshot = "X", Action = "a", Timestamp = DateTime.UtcNow.AddSeconds(-i) })
                .Append(new ActivityLogEntry { Id = 999, EntityType = "Project", EntityId = 1, EntityNameSnapshot = "my quiet project", ActorNameSnapshot = "X", Action = "a", Timestamp = DateTime.UtcNow.AddDays(-3) })
                .ToList();
            var log = Substitute.For<IRepository<ActivityLogEntry>>();
            log.Query().Returns(_ => entries.BuildMock());
            var sut = new ActivityLogService(log, Substitute.For<IRepository<User>>(), Substitute.For<IUnitOfWork>(), Member(), Substitute.For<IRepository<ProjectTask>>());

            var feed = await sut.GetRecentAsync(5);

            Assert.Equal(new[] { "my quiet project" }, feed.Select(f => f.EntityNameSnapshot));
        }

        private static (TimesheetService Sut, IRepository<Timesheet> Timesheets) Timesheets(Timesheet stored, IProjectScope scope)
        {
            var timesheets = Substitute.For<IRepository<Timesheet>>();
            timesheets.Query().Returns(_ => new List<Timesheet> { stored }.BuildMock());
            var sut = new TimesheetService(timesheets, Substitute.For<IRepository<TimesheetEntry>>(), Substitute.For<IRepository<User>>(),
                Substitute.For<IRepository<UserRole>>(), Substitute.For<IRepository<RolePermission>>(), Substitute.For<INotificationService>(),
                Substitute.For<IEmailService>(), Substitute.For<IUnitOfWork>(), scope);
            return (sut, timesheets);
        }

        private static Timesheet StoredTimesheet() => new()
        {
            Id = 1, UserId = 50, User = new User { Id = 50, Name = "Owner" }, Status = "Draft",
            PeriodStart = DateTime.UtcNow.Date, PeriodEnd = DateTime.UtcNow.Date.AddDays(6),
            Entries = new List<TimesheetEntry>
            {
                new() { Id = 1, ProjectId = 1, Project = new Project { Id = 1, Name = "Mine" }, Hours = 1, EntryDate = DateTime.UtcNow.Date },
                new() { Id = 2, ProjectId = 5, Project = new Project { Id = 5, Name = "Secret" }, Hours = 2, EntryDate = DateTime.UtcNow.Date },
            },
        };

        [Fact]
        public async Task AReviewerNeverLearnsTheNameOfAProjectTheyAreNotOn()
        {
            var (sut, _) = Timesheets(StoredTimesheet(), Member());
            var reviewer = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, ScopeFixture.Me.ToString()), new Claim("permission", "timesheets.approve"),
            }));

            var dto = await sut.GetTimesheetByIdAsync(1, reviewer);

            Assert.Equal(new[] { "Mine", "Restricted project" }, dto!.Entries.Select(e => e.ProjectName));
        }

        [Fact]
        public async Task AnAdministratorSeesEveryProjectNameOnATimesheet()
        {
            var (sut, _) = Timesheets(StoredTimesheet(), ScopeFixture.For(ScopeFixture.Me, administrator: true));
            var reviewer = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, ScopeFixture.Me.ToString()), new Claim("permission", "timesheets.view_all"),
            }));

            var dto = await sut.GetTimesheetByIdAsync(1, reviewer);

            Assert.Equal(new[] { "Mine", "Secret" }, dto!.Entries.Select(e => e.ProjectName));
        }

        [Fact]
        public async Task EditingATimesheet_KeepsAnEntryOnAProjectThePersonHasSinceLeft_ButNotANewOne()
        {
            var stored = StoredTimesheet();
            stored.UserId = ScopeFixture.Me;      // the owner, who left project 5 (they are on project 1 only)
            var (sut, _) = Timesheets(stored, Member());
            var owner = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, ScopeFixture.Me.ToString()) }));
            CreateTimesheetEntryDto Entry(int project) => new() { EntryDate = DateTime.UtcNow.Date, ProjectId = project, Hours = 1, Description = "d" };

            // Still lists the old project-5 entry alongside a normal one: allowed.
            Assert.True(await sut.UpdateTimesheetAsync(1, new UpdateTimesheetDto { Entries = new List<CreateTimesheetEntryDto> { Entry(1), Entry(5) } }, owner));

            // A project-7 entry is new to this timesheet and not theirs: refused.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.UpdateTimesheetAsync(1, new UpdateTimesheetDto { Entries = new List<CreateTimesheetEntryDto> { Entry(7) } }, owner));
        }
    }
}
