using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Administrators inside Project Management: a SuperAdmin (the "superadmin" claim) and - equally - a Project Admin of
    // at least one project. ResolveCallerAsync turns both into the same PmCaller, so every existing "is this a
    // SuperAdmin" check in the module applies to them without further change.
    public class PmAdministratorTests
    {
        private readonly IRepository<ProjectUser> _members = Substitute.For<IRepository<ProjectUser>>();
        private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
        private readonly IRepository<Project> _projects = Substitute.For<IRepository<Project>>();

        private ProjectAccessService CreateSut() => new(_members, _users, _projects);

        private static User Person(int id, bool active = true, bool access = true, bool readOnly = false) =>
            new() { Id = id, IsActive = active, PmAccess = access, PmReadOnly = readOnly };

        private void Members(params (int UserId, string Role, User User)[] rows) =>
            _members.Query().Returns(_ => rows.Select(r => new ProjectUser { ProjectId = 1, UserId = r.UserId, ProjectRole = r.Role, User = r.User }).ToList().BuildMock());

        [Fact]
        public async Task ASuperAdminClaim_IsAnAdministrator_WithoutTouchingTheDatabase()
        {
            var caller = await CreateSut().ResolveCallerAsync(7, hasSuperAdminClaim: true, hasPmAdminPermission: false);

            Assert.True(caller.IsSuperAdmin);
            Assert.True(caller.IsPmAdmin);
            _members.DidNotReceiveWithAnyArgs().Query();
        }

        [Fact]
        public async Task AProjectAdmin_IsTreatedExactlyLikeASuperAdmin()
        {
            Members((7, ProjectRoles.ProjectAdmin, Person(7)));

            var caller = await CreateSut().ResolveCallerAsync(7, false, false);

            Assert.Equal(new PmCaller(7, true, true), caller);
        }

        [Fact]
        public async Task BeingProjectAdminOfJustOneProject_IsEnough_WhateverTheRoleElsewhere()
        {
            var me = Person(7);
            Members((7, ProjectRoles.Developer, me), (7, ProjectRoles.Tester, me), (7, ProjectRoles.ProjectAdmin, me));

            Assert.True((await CreateSut().ResolveCallerAsync(7, false, false)).IsSuperAdmin);
        }

        [Theory]
        [InlineData(ProjectRoles.ProjectManager)]
        [InlineData(ProjectRoles.ScrumMaster)]
        [InlineData(ProjectRoles.Developer)]
        [InlineData(ProjectRoles.Tester)]
        [InlineData(ProjectRoles.Viewer)]
        public async Task EveryOtherProjectRole_IsNotAnAdministrator(string role)
        {
            Members((7, role, Person(7)));

            var caller = await CreateSut().ResolveCallerAsync(7, false, false);

            Assert.False(caller.IsSuperAdmin);
            Assert.False(caller.IsPmAdmin);
        }

        [Fact]
        public async Task ProjectAdminWhoNoLongerHasModuleAccess_IsNotAnAdministrator()
        {
            Members((7, ProjectRoles.ProjectAdmin, Person(7, access: false)));
            Assert.False((await CreateSut().ResolveCallerAsync(7, false, false)).IsSuperAdmin);
        }

        [Fact]
        public async Task ProjectAdminWhoWasCappedAtReadOnly_IsNotAnAdministrator()
        {
            // A SuperAdmin's explicit "read only" restriction still wins over the Project Admin role.
            Members((7, ProjectRoles.ProjectAdmin, Person(7, readOnly: true)));
            Assert.False((await CreateSut().ResolveCallerAsync(7, false, false)).IsSuperAdmin);
        }

        [Fact]
        public async Task ADeactivatedProjectAdmin_IsNotAnAdministrator()
        {
            Members((7, ProjectRoles.ProjectAdmin, Person(7, active: false)));
            Assert.False((await CreateSut().ResolveCallerAsync(7, false, false)).IsSuperAdmin);
        }

        [Fact]
        public async Task SomeoneElsesAdminRow_DoesNotMakeTheCallerAnAdministrator()
        {
            Members((8, ProjectRoles.ProjectAdmin, Person(8)));
            Assert.False((await CreateSut().ResolveCallerAsync(7, false, false)).IsSuperAdmin);
        }

        [Fact]
        public async Task APmAdminPermissionHolder_KeepsTheirModuleAdminRights_WithoutBecomingAllPowerful()
        {
            Members();   // not a Project Admin anywhere

            var caller = await CreateSut().ResolveCallerAsync(7, false, hasPmAdminPermission: true);

            Assert.False(caller.IsSuperAdmin);
            Assert.True(caller.IsPmAdmin);     // may still grant module access, as before
        }

        // ---- what "equal" means in practice: the same checks a SuperAdmin passes

        [Fact]
        public async Task AnElevatedProjectAdmin_CanActInAProjectTheyAreNotOn_AndIsNeverReadOnly()
        {
            Members((7, ProjectRoles.ProjectAdmin, Person(7)));
            _users.Query().Returns(_ => new List<User> { Person(7) }.BuildMock());
            _projects.Query().Returns(_ => new List<Project> { new() { Id = 1 }, new() { Id = 2 } }.BuildMock());
            var sut = CreateSut();
            var caller = await sut.ResolveCallerAsync(7, false, false);

            // project 2 has no membership row for them at all
            await sut.RequireAsync(caller, 2, PmAction.DeleteItem);
            await sut.RequireAsync(caller, 2, PmAction.ManageMembers);
            Assert.True(await sut.HasModuleAccessAsync(caller));
            Assert.False(await sut.IsReadOnlyAsync(caller));
            Assert.Equal(ProjectAccessService.SuperAdminRole, await sut.GetRoleAsync(caller, 2));
        }

        [Fact]
        public async Task AnElevatedProjectAdmin_StillGetsNotFoundForAProjectThatDoesNotExist()
        {
            Members((7, ProjectRoles.ProjectAdmin, Person(7)));
            _projects.Query().Returns(_ => new List<Project> { new() { Id = 1 } }.BuildMock());
            var sut = CreateSut();
            var caller = await sut.ResolveCallerAsync(7, false, false);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.RequireAsync(caller, 404, PmAction.View));
        }
    }
}
