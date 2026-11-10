using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Builds a REAL ProjectScope for a given caller, so tests of the services prove the actual restriction rather than
    // a hand-written fake of it.
    internal static class ScopeFixture
    {
        public const int Me = 10;

        public static ProjectScope For(
            int? userId, bool administrator = false,
            (int ProjectId, string Role)[]? memberships = null, int[]? created = null, bool readOnly = false)
        {
            var context = Substitute.For<ICurrentRequestContext>();
            context.UserId.Returns(userId);
            context.IsAdministrator.Returns(administrator);

            var owner = userId ?? 0;
            var projects = Substitute.For<IRepository<Project>>();
            projects.Query().Returns(_ => (created ?? Array.Empty<int>()).Select(id => new Project { Id = id, CreatedBy = owner })
                .Concat(new[] { new Project { Id = 900, CreatedBy = 999 } })              // someone else's project
                .ToList().BuildMock());

            var members = Substitute.For<IRepository<ProjectUser>>();
            members.Query().Returns(_ => (memberships ?? Array.Empty<(int, string)>()).Select(m => new ProjectUser { ProjectId = m.ProjectId, UserId = owner, ProjectRole = m.Role })
                .Concat(new[] { new ProjectUser { ProjectId = 901, UserId = 999, ProjectRole = ProjectRoles.ProjectAdmin } })   // someone else's membership
                .ToList().BuildMock());

            var users = Substitute.For<IRepository<User>>();
            users.Query().Returns(_ => new List<User> { new() { Id = owner, PmReadOnly = readOnly } }.BuildMock());

            return new ProjectScope(context, projects, members, users);
        }
    }

    public class ProjectScopeTests
    {
        private static readonly (int, string)[] OnProjects =
        {
            (1, ProjectRoles.Developer), (2, ProjectRoles.Viewer), (3, ProjectRoles.Tester), (4, ProjectRoles.ProjectAdmin),
        };

        [Fact]
        public async Task WithNoSignedInUser_ItIsTheSystem_AndNothingIsRestricted()
        {
            var scope = ScopeFixture.For(userId: null);
            Assert.True(await scope.IsUnrestrictedAsync());
            Assert.Null(await scope.VisibleProjectIdsAsync());
            Assert.Equal(ProjectAccessOutcome.Allowed, await scope.CheckAsync(123, ProjectAbility.Edit));
        }

        [Fact]
        public async Task AnAdministrator_SeesAndMayDoEverything()
        {
            var scope = ScopeFixture.For(ScopeFixture.Me, administrator: true);
            Assert.True(await scope.IsUnrestrictedAsync());
            Assert.Null(await scope.VisibleProjectIdsAsync());
            Assert.Equal(ProjectAccessOutcome.Allowed, await scope.CheckAsync(777, ProjectAbility.Edit));
        }

        [Fact]
        public async Task AnOrdinaryUser_SeesOnlyTheirOwnProjects_MemberOrCreator()
        {
            var scope = ScopeFixture.For(ScopeFixture.Me, memberships: OnProjects, created: new[] { 50 });

            var visible = await scope.VisibleProjectIdsAsync();

            Assert.NotNull(visible);
            Assert.Equal(new[] { 1, 2, 3, 4, 50 }, visible!.OrderBy(i => i));
            Assert.DoesNotContain(900, visible);    // another person's project
            Assert.DoesNotContain(901, visible);    // a project only someone else belongs to
            Assert.False(await scope.IsUnrestrictedAsync());
        }

        [Fact]
        public async Task ScopingProjectsAndTasks_DropsEverythingOutsideTheCallersProjects()
        {
            var scope = ScopeFixture.For(ScopeFixture.Me, memberships: OnProjects);
            var projects = new List<Project> { new() { Id = 1 }, new() { Id = 5 }, new() { Id = 900 } }.BuildMock();
            var tasks = new List<ProjectTask> { new() { Id = 1, ProjectId = 1 }, new() { Id = 2, ProjectId = 5 }, new() { Id = 3, ProjectId = 900 } }.BuildMock();

            Assert.Equal(new[] { 1 }, (await scope.ScopeAsync(projects)).Select(p => p.Id));
            Assert.Equal(new[] { 1 }, (await scope.ScopeAsync(tasks)).Select(t => t.Id));
        }

        [Theory]
        [InlineData(ProjectAbility.View)]
        [InlineData(ProjectAbility.Create)]
        [InlineData(ProjectAbility.Edit)]
        public async Task AProjectTheCallerIsNotOn_IsNotVisible_ForEveryAbility(ProjectAbility ability)
        {
            var scope = ScopeFixture.For(ScopeFixture.Me, memberships: OnProjects);

            Assert.Equal(ProjectAccessOutcome.NotVisible, await scope.CheckAsync(5, ability));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => scope.RequireAsync(5, ability));
        }

        [Theory]
        [InlineData(1, ProjectAbility.View, ProjectAccessOutcome.Allowed)]       // Developer
        [InlineData(1, ProjectAbility.Create, ProjectAccessOutcome.Allowed)]
        [InlineData(1, ProjectAbility.Edit, ProjectAccessOutcome.Allowed)]
        [InlineData(2, ProjectAbility.View, ProjectAccessOutcome.Allowed)]       // Viewer
        [InlineData(2, ProjectAbility.Create, ProjectAccessOutcome.Forbidden)]
        [InlineData(2, ProjectAbility.Edit, ProjectAccessOutcome.Forbidden)]
        [InlineData(3, ProjectAbility.Create, ProjectAccessOutcome.Allowed)]     // Tester
        [InlineData(3, ProjectAbility.Edit, ProjectAccessOutcome.Allowed)]
        [InlineData(4, ProjectAbility.Edit, ProjectAccessOutcome.Allowed)]       // Project Admin
        public async Task WhatAMemberMayDo_FollowsTheirProjectRole(int projectId, ProjectAbility ability, ProjectAccessOutcome expected)
        {
            var scope = ScopeFixture.For(ScopeFixture.Me, memberships: OnProjects);
            Assert.Equal(expected, await scope.CheckAsync(projectId, ability));
        }

        [Fact]
        public async Task AViewerIsToldFlatlyNo_NotThatTheProjectDoesNotExist()
        {
            var scope = ScopeFixture.For(ScopeFixture.Me, memberships: OnProjects);
            await Assert.ThrowsAsync<ForbiddenException>(() => scope.RequireAsync(2, ProjectAbility.Create));
        }

        [Theory]
        [InlineData(ProjectAbility.Create)]
        [InlineData(ProjectAbility.Edit)]
        public async Task ReadOnlyAccess_CanViewButNeverChange_EvenAsProjectAdmin(ProjectAbility ability)
        {
            var scope = ScopeFixture.For(ScopeFixture.Me, memberships: OnProjects, readOnly: true);

            Assert.Equal(ProjectAccessOutcome.Allowed, await scope.CheckAsync(4, ProjectAbility.View));
            Assert.Equal(ProjectAccessOutcome.Forbidden, await scope.CheckAsync(4, ability));
        }

        [Fact]
        public async Task TheCreatorOfAProject_IsItsOwner_EvenWithoutATeamRow()
        {
            var scope = ScopeFixture.For(ScopeFixture.Me, created: new[] { 50 });

            Assert.Equal(ProjectAccessOutcome.Allowed, await scope.CheckAsync(50, ProjectAbility.View));
            Assert.Equal(ProjectAccessOutcome.Allowed, await scope.CheckAsync(50, ProjectAbility.Edit));
            Assert.Equal(ProjectAccessOutcome.NotVisible, await scope.CheckAsync(51, ProjectAbility.View));
        }

        [Fact]
        public async Task TheLookupHappensOnce_NotOncePerCheck()
        {
            var context = Substitute.For<ICurrentRequestContext>();
            context.UserId.Returns(ScopeFixture.Me);
            var members = Substitute.For<IRepository<ProjectUser>>();
            members.Query().Returns(_ => new List<ProjectUser>().BuildMock());
            var projects = Substitute.For<IRepository<Project>>();
            projects.Query().Returns(_ => new List<Project>().BuildMock());
            var users = Substitute.For<IRepository<User>>();
            users.Query().Returns(_ => new List<User>().BuildMock());
            var scope = new ProjectScope(context, projects, members, users);

            await scope.CheckAsync(1, ProjectAbility.View);
            await scope.CheckAsync(2, ProjectAbility.Edit);
            await scope.VisibleProjectIdsAsync();

            members.Received(1).Query();
        }
    }
}
