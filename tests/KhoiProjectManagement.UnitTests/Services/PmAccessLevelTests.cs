using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // The per-user read / read-and-write policy: set by a SuperAdmin (or on import), enforced in
    // ProjectAccessService, and surfaced on the project list and the member list so the UI can show it.
    public class PmAccessLevelTests
    {
        private static readonly PmCaller Dev = new(10, false);
        private static readonly PmCaller Super = new(2, true, true);

        private readonly IRepository<Project> _projects = Substitute.For<IRepository<Project>>();
        private readonly IRepository<ProjectUser> _members = Substitute.For<IRepository<ProjectUser>>();
        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
        private readonly IRepository<UserRole> _userRoles = Substitute.For<IRepository<UserRole>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly IActivityLogService _activity = Substitute.For<IActivityLogService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        public PmAccessLevelTests()
        {
            _access.HasModuleAccessAsync(default!).ReturnsForAnyArgs(true);
            _userRoles.Query().Returns(_ => new List<UserRole>().BuildMock());
        }

        private PmProjectService CreateSut() => new(_projects, _members, _tasks, _users, _userRoles, _access, _activity, _uow);

        // ---- reading the level

        [Fact]
        public async Task MyProjects_CarryTheCallersReadOnlyLevel()
        {
            _access.IsReadOnlyAsync(default!).ReturnsForAnyArgs(true);
            _projects.Query().Returns(_ => new List<Project>
            {
                new() { Id = 1, Name = "Alpha", Status = "active", ProjectUsers = { new ProjectUser { UserId = 10, ProjectRole = ProjectRoles.Developer } } },
                new() { Id = 2, Name = "Beta", Status = "active", ProjectUsers = { new ProjectUser { UserId = 99, ProjectRole = ProjectRoles.Developer } } },
            }.BuildMock());

            var mine = await CreateSut().GetMyProjectsAsync(Dev);

            var alpha = Assert.Single(mine);
            Assert.Equal("Alpha", alpha.Name);
            Assert.True(alpha.ReadOnly);
            Assert.Equal(ProjectRoles.Developer, alpha.MyRole);   // the role is still reported; the level is separate
        }

        [Fact]
        public async Task MyProjects_ForAReadWritePerson_AreNotReadOnly()
        {
            _projects.Query().Returns(_ => new List<Project>
            {
                new() { Id = 1, Name = "Alpha", ProjectUsers = { new ProjectUser { UserId = 10, ProjectRole = ProjectRoles.Tester } } },
            }.BuildMock());

            Assert.False(Assert.Single(await CreateSut().GetMyProjectsAsync(Dev)).ReadOnly);
        }

        [Fact]
        public async Task Members_ShowEachPersonsModuleLevel()
        {
            _members.Query().Returns(_ => new List<ProjectUser>
            {
                new() { ProjectId = 1, UserId = 1, ProjectRole = ProjectRoles.Developer, User = new User { Id = 1, Name = "Ada", Email = "a@x.com", PmReadOnly = false } },
                new() { ProjectId = 1, UserId = 2, ProjectRole = ProjectRoles.Developer, User = new User { Id = 2, Name = "Bo", Email = "b@x.com", PmReadOnly = true } },
            }.BuildMock());

            var members = await CreateSut().GetMembersAsync(Dev, 1);

            Assert.Equal(new[] { false, true }, members.Select(m => m.ModuleReadOnly));
        }

        // ---- setting the level

        private User ExistingUser(bool access = false, bool only = false, bool readOnly = false)
        {
            var user = new User { Id = 5, Name = "Pat", PmAccess = access, PmOnly = only, PmReadOnly = readOnly, IsActive = true };
            _users.Query().Returns(_ => new List<User> { user }.BuildMock());
            return user;
        }

        [Fact]
        public async Task SetAccess_ReadOnly_IsStored()
        {
            var user = ExistingUser();

            await CreateSut().SetAccessAsync(2, 5, new SetPmAccessDto { PmAccess = true, ReadOnly = true });

            Assert.True(user.PmAccess);
            Assert.True(user.PmReadOnly);
            await _activity.Received(1).LogAsync("User", 5, "Pat", 2, "pm-access-set", Arg.Is<string?>(d => d!.Contains("read-only")));
        }

        [Fact]
        public async Task SetAccess_ReadAndWrite_ClearsAnEarlierReadOnlyLevel()
        {
            var user = ExistingUser(access: true, readOnly: true);

            await CreateSut().SetAccessAsync(2, 5, new SetPmAccessDto { PmAccess = true, ReadOnly = false });

            Assert.False(user.PmReadOnly);
        }

        [Fact]
        public async Task SetAccess_RevokingAccess_AlsoDropsTheLevelAndTheConfinement()
        {
            var user = ExistingUser(access: true, only: false, readOnly: true);

            await CreateSut().SetAccessAsync(2, 5, new SetPmAccessDto { PmAccess = false, PmOnly = false, ReadOnly = false });

            Assert.False(user.PmAccess);
            Assert.False(user.PmReadOnly);
        }

        [Fact]
        public async Task SetAccess_ReadOnlyWithoutAccess_IsRejected()
        {
            var user = ExistingUser();

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SetAccessAsync(2, 5, new SetPmAccessDto { PmAccess = false, ReadOnly = true }));

            Assert.False(user.PmReadOnly);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        // ---- import

        private void ImportFixture(out User pat)
        {
            pat = new User { Id = 12, Name = "Pat", Email = "pat@x.com", IsActive = true, PmAccess = false };
            var users = new List<User> { pat };
            _users.Query().Returns(_ => users.BuildMock());
            _members.Query().Returns(_ => new List<ProjectUser> { new() { ProjectId = 1, UserId = 1, ProjectRole = ProjectRoles.ProjectAdmin } }.BuildMock());
        }

        [Fact]
        public async Task Import_CanGrantReadOnlyAccess()
        {
            ImportFixture(out var pat);

            var result = await CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                GrantModuleAccess = true, ReadOnlyAccess = true, Rows = { new ImportMemberRowDto { Email = "pat@x.com", Role = "Developer" } },
            });

            Assert.True(pat.PmAccess);
            Assert.True(pat.PmReadOnly);
            Assert.Contains("Read-only access", result.Rows[0].Message);
        }

        [Fact]
        public async Task Import_GrantsReadAndWriteByDefault()
        {
            ImportFixture(out var pat);

            await CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                GrantModuleAccess = true, Rows = { new ImportMemberRowDto { Email = "pat@x.com" } },
            });

            Assert.True(pat.PmAccess);
            Assert.False(pat.PmReadOnly);
        }

        [Fact]
        public async Task Import_ReadOnlyWithoutAGrant_IsRejected()
        {
            ImportFixture(out _);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                ReadOnlyAccess = true, Rows = { new ImportMemberRowDto { Email = "pat@x.com" } },
            }));
        }

        [Fact]
        public async Task Import_DryRun_DoesNotChangeTheLevel()
        {
            ImportFixture(out var pat);

            await CreateSut().ImportMembersAsync(Super, 1, new ImportMembersDto
            {
                DryRun = true, GrantModuleAccess = true, ReadOnlyAccess = true, Rows = { new ImportMemberRowDto { Email = "pat@x.com" } },
            });

            Assert.False(pat.PmAccess);
            Assert.False(pat.PmReadOnly);
        }

        // ---- a project's creator must be able to find their project

        [Fact]
        public async Task MyProjects_AddsTheCreatorAsProjectAdmin_WhenTheyAreNotOnTheTeam()
        {
            var added = new List<ProjectUser>();
            _members.When(r => r.Add(Arg.Any<ProjectUser>())).Do(ci => added.Add(ci.Arg<ProjectUser>()));
            _projects.Query().Returns(_ => new List<Project>
            {
                new() { Id = 1, Name = "Mine, not on the team", CreatedBy = 10 },
                new() { Id = 2, Name = "Mine and on the team", CreatedBy = 10, ProjectUsers = { new ProjectUser { UserId = 10, ProjectRole = ProjectRoles.Developer } } },
                new() { Id = 3, Name = "Someone else's", CreatedBy = 99 },
            }.BuildMock());

            await CreateSut().GetMyProjectsAsync(Dev);

            var row = Assert.Single(added);
            Assert.Equal((1, 10, ProjectRoles.ProjectAdmin), (row.ProjectId, row.UserId, row.ProjectRole));
            await _uow.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task MyProjects_WhenTheCreatorIsAlreadyAMember_WritesNothing()
        {
            _projects.Query().Returns(_ => new List<Project>
            {
                new() { Id = 2, Name = "Mine", CreatedBy = 10, ProjectUsers = { new ProjectUser { UserId = 10, ProjectRole = ProjectRoles.Tester } } },
            }.BuildMock());

            await CreateSut().GetMyProjectsAsync(Dev);

            _members.DidNotReceiveWithAnyArgs().Add(default!);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task MyProjects_ASuperAdminIsNeverAddedAsAMember()
        {
            _projects.Query().Returns(_ => new List<Project> { new() { Id = 1, Name = "Any", CreatedBy = 2 } }.BuildMock());

            await CreateSut().GetMyProjectsAsync(Super);

            _members.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task MyProjects_ARacingRequestThatAddedTheCreatorFirst_IsNotAnError()
        {
            _projects.Query().Returns(_ => new List<Project> { new() { Id = 1, Name = "Mine", CreatedBy = 10 } }.BuildMock());
            _uow.SaveChangesAsync().Returns<int>(_ => throw new Microsoft.EntityFrameworkCore.DbUpdateException("duplicate key"));

            var mine = await CreateSut().GetMyProjectsAsync(Dev);

            Assert.NotNull(mine);
        }

        [Fact]
        public async Task AccessAdmin_ListsHowManyProjectsEachPersonIsOn()
        {
            _users.Query().Returns(_ => new List<User>
            {
                new() { Id = 1, Name = "Ada", Email = "a@x.com", IsActive = true, PmAccess = false, ProjectUsers = { new ProjectUser(), new ProjectUser() } },
                new() { Id = 2, Name = "Bo", Email = "b@x.com", IsActive = true, PmAccess = true },
            }.BuildMock());

            var people = await CreateSut().GetAccessUsersAsync();

            Assert.Equal(new[] { 2, 0 }, people.Select(p => p.ProjectCount));
        }
    }
}
