using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Need-to-know: a person sees and changes only the projects they belong to. Being Project Admin of one project is NOT
    // an administrator of the module - it must grant nothing on any other project. (An earlier version of the module made it
    // one; that let anyone who had created a project see, and create tickets on, every other project.)
    public class PmAdministratorTests
    {
        private readonly IRepository<ProjectUser> _members = Substitute.For<IRepository<ProjectUser>>();
        private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
        private readonly IRepository<Project> _projects = Substitute.For<IRepository<Project>>();

        private ProjectAccessService CreateSut() => new(_members, _users, _projects);

        // The caller is a Project Admin of project 1 and has no membership of project 2.
        private void ProjectAdminOfProjectOneOnly()
        {
            _users.Query().Returns(_ => new List<User> { new() { Id = 7, IsActive = true, PmAccess = true } }.BuildMock());
            _members.Query().Returns(_ => new List<ProjectUser> { new() { ProjectId = 1, UserId = 7, ProjectRole = ProjectRoles.ProjectAdmin } }.BuildMock());
            _projects.Query().Returns(_ => new List<Project> { new() { Id = 1 }, new() { Id = 2 } }.BuildMock());
        }

        [Fact]
        public async Task AProjectAdmin_HasFullControlOfTheirOwnProject()
        {
            ProjectAdminOfProjectOneOnly();
            var caller = new PmCaller(7, false);
            var sut = CreateSut();

            await sut.RequireAsync(caller, 1, PmAction.DeleteItem);
            await sut.RequireAsync(caller, 1, PmAction.ManageMembers);
            Assert.Equal(ProjectRoles.ProjectAdmin, await sut.GetRoleAsync(caller, 1));
        }

        [Theory]
        [InlineData(PmAction.View)]
        [InlineData(PmAction.CreateItem)]
        [InlineData(PmAction.EditItem)]
        [InlineData(PmAction.DeleteItem)]
        [InlineData(PmAction.ManageMembers)]
        [InlineData(PmAction.Collaborate)]
        public async Task AProjectAdmin_CannotSeeOrTouchAProjectTheyAreNotOn(PmAction action)
        {
            ProjectAdminOfProjectOneOnly();

            // 404, not 403: the project's existence is not revealed either.
            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().RequireAsync(new PmCaller(7, false), 2, action));
        }

        [Fact]
        public async Task AProjectAdmin_HasNoRoleInAnotherProject()
        {
            ProjectAdminOfProjectOneOnly();
            Assert.Null(await CreateSut().GetRoleAsync(new PmCaller(7, false), 2));
        }

        [Fact]
        public async Task ASuperAdmin_StillSeesEveryProject()
        {
            ProjectAdminOfProjectOneOnly();
            var caller = new PmCaller(7, true, true);

            await CreateSut().RequireAsync(caller, 2, PmAction.DeleteItem);
            Assert.Equal(ProjectAccessService.SuperAdminRole, await CreateSut().GetRoleAsync(caller, 2));
        }
    }
}
