using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class ProjectRolesTests
    {
        [Theory]
        [InlineData(ProjectRoles.ProjectAdmin, PmAction.ManageMembers, true)]
        [InlineData(ProjectRoles.ProjectManager, PmAction.ManageMembers, false)]
        [InlineData(ProjectRoles.ScrumMaster, PmAction.DeleteItem, true)]
        [InlineData(ProjectRoles.Developer, PmAction.DeleteItem, false)]
        [InlineData(ProjectRoles.Developer, PmAction.RankItem, true)]
        [InlineData(ProjectRoles.Tester, PmAction.EditItem, true)]
        [InlineData(ProjectRoles.Tester, PmAction.RankItem, false)]
        [InlineData(ProjectRoles.Viewer, PmAction.View, true)]
        [InlineData(ProjectRoles.ScrumMaster, PmAction.ManageBoard, true)]
        [InlineData(ProjectRoles.ProjectManager, PmAction.ManageBoard, true)]
        [InlineData(ProjectRoles.Developer, PmAction.ManageBoard, false)]
        [InlineData(ProjectRoles.Viewer, PmAction.CreateItem, false)]
        [InlineData(ProjectRoles.Tester, PmAction.Collaborate, true)]
        [InlineData(ProjectRoles.Developer, PmAction.Collaborate, true)]
        [InlineData(ProjectRoles.Viewer, PmAction.Collaborate, false)]
        [InlineData("Nonsense", PmAction.View, false)]
        [InlineData(null, PmAction.View, false)]
        public void Allows_FollowsTheRoleMatrix(string? role, PmAction action, bool expected) =>
            Assert.Equal(expected, ProjectRoles.Allows(role, action));
    }

    public class WorkItemHierarchyTests
    {
        [Theory]
        [InlineData("Epic", null, true)]
        [InlineData("Feature", "Epic", true)]
        [InlineData("Story", "Feature", true)]
        [InlineData("Story", "Epic", true)]
        [InlineData("Task", "Story", true)]
        [InlineData("Bug", "Story", true)]
        [InlineData("Subtask", "Task", true)]
        [InlineData("Subtask", null, false)]
        [InlineData("Epic", "Story", false)]
        [InlineData("Story", "Task", false)]
        [InlineData("Task", "Task", false)]
        [InlineData("Task", "Bug", false)]
        [InlineData("Meeting", "Epic", false)]
        [InlineData("Story", "Meeting", false)]
        public void ValidateParent_EnforcesStrictlyDescendingLevels(string child, string? parent, bool valid) =>
            Assert.Equal(valid, WorkItemHierarchy.ValidateParent(child, parent) == null);
    }

    public class PmWorkItemServiceTests
    {
        private static readonly PmCaller Dev = new(10, false);

        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<Project> _projects = Substitute.For<IRepository<Project>>();
        private readonly IRepository<ProjectUser> _members = Substitute.For<IRepository<ProjectUser>>();
        private readonly IRepository<TaskStatusHistory> _history = Substitute.For<IRepository<TaskStatusHistory>>();
        private readonly IRepository<Attachment> _attachments = Substitute.For<IRepository<Attachment>>();
        private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
        private readonly IPmWorkflowService _workflow = Substitute.For<IPmWorkflowService>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly IActivityLogService _activity = Substitute.For<IActivityLogService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        private PmWorkItemService CreateSut() => new(_tasks, _projects, _members, _history, _attachments, _storage, _workflow, _access, _activity, _uow);

        private void SetTasks(params ProjectTask[] items) => _tasks.Query().Returns(_ => items.ToList().BuildMock());

        [Fact]
        public async Task CreateAsync_WithoutPermission_Throws_AndWritesNothing()
        {
            _access.RequireAsync(Dev, 1, PmAction.CreateItem).Returns(Task.FromException(new ForbiddenException()));

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                CreateSut().CreateAsync(Dev, 1, new SaveWorkItemDto { Title = "x", Type = "Story" }));

            _tasks.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task CreateAsync_RejectsIllegalParentType()
        {
            _projects.Query().Returns(new List<Project> { new() { Id = 1, Name = "P" } }.BuildMock());
            SetTasks(new ProjectTask { Id = 5, ProjectId = 1, Type = "Task" });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().CreateAsync(Dev, 1, new SaveWorkItemDto { Title = "x", Type = "Story", ParentId = 5 }));

            Assert.Contains("cannot be placed under", ex.Message);
            _tasks.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task CreateAsync_RejectsAParentFromAnotherProject()
        {
            _projects.Query().Returns(new List<Project> { new() { Id = 1, Name = "P" } }.BuildMock());
            SetTasks(new ProjectTask { Id = 5, ProjectId = 2, Type = "Epic" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().CreateAsync(Dev, 1, new SaveWorkItemDto { Title = "x", Type = "Feature", ParentId = 5 }));
        }

        [Fact]
        public async Task CreateAsync_RejectsAssigneeWhoIsNotAProjectMember()
        {
            _projects.Query().Returns(new List<Project> { new() { Id = 1, Name = "P" } }.BuildMock());
            SetTasks();
            _members.Query().Returns(new List<ProjectUser>().BuildMock());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().CreateAsync(Dev, 1, new SaveWorkItemDto { Title = "x", Type = "Task", AssignedToId = 99 }));

            Assert.Contains("not a member", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_Valid_AppendsAtEndOfBacklog_DefaultsDueDateToProjectEnd_AndLogsActivity()
        {
            var end = new DateTime(2027, 1, 31);
            _projects.Query().Returns(new List<Project> { new() { Id = 1, Name = "P", EndDate = end } }.BuildMock());
            var existing = new ProjectTask { Id = 5, ProjectId = 1, Type = "Epic", Title = "E", Rank = 3000 };
            SetTasks(existing);
            ProjectTask? added = null;
            _tasks.When(r => r.Add(Arg.Any<ProjectTask>())).Do(ci =>
            {
                added = ci.Arg<ProjectTask>();
                added.Id = 6;
                SetTasks(existing, added);
            });

            var result = await CreateSut().CreateAsync(Dev, 1, new SaveWorkItemDto { Title = "  Feat  ", Type = "Feature", ParentId = 5 });

            Assert.Equal("Feat", added!.Title);
            Assert.Equal(4000, added.Rank);
            Assert.Equal(end, added.DueDate);
            Assert.Equal(6, result.Id);
            await _activity.Received(1).LogAsync("WorkItem", 6, "Feat", Dev.UserId, "created", Arg.Any<string?>());
        }

        [Fact]
        public async Task RankAsync_PlacesBetweenTheTwoNeighbours()
        {
            var moving = new ProjectTask { Id = 3, ProjectId = 1, Type = "Task", Rank = 5000 };
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Rank = 1000 },
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Rank = 2000 },
                moving);

            await CreateSut().RankAsync(Dev, 3, new RankWorkItemDto { AfterItemId = 1 });

            Assert.Equal(1500, moving.Rank);
        }

        [Fact]
        public async Task RankAsync_WithNullAfter_MovesToTop()
        {
            var moving = new ProjectTask { Id = 3, ProjectId = 1, Type = "Task", Rank = 5000 };
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Rank = 1000 }, moving);

            await CreateSut().RankAsync(Dev, 3, new RankWorkItemDto { AfterItemId = null });

            Assert.Equal(0, moving.Rank);
        }

        [Fact]
        public async Task DeleteAsync_WithChildren_IsRejected()
        {
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Epic", Title = "E" },
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Feature", ParentId = 1 });

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().DeleteAsync(Dev, 1));
            _tasks.DidNotReceiveWithAnyArgs().Remove(default!);
        }

        [Fact]
        public async Task DeleteAsync_RemovesAttachmentRowsAndStoredFiles()
        {
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Title = "T" });
            var files = new[] { new Attachment { Id = 5, TaskId = 1, FilePath = "pm/1/a.png" }, new Attachment { Id = 6, TaskId = 1, FilePath = "pm/1/b.pdf" } };
            _attachments.Query().Returns(_ => files.ToList().BuildMock());

            await CreateSut().DeleteAsync(Dev, 1);

            _attachments.Received(1).RemoveRange(Arg.Any<IEnumerable<Attachment>>());
            await _storage.Received(1).DeleteAsync("pm/1/a.png", Arg.Any<CancellationToken>());
            await _storage.Received(1).DeleteAsync("pm/1/b.pdf", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task UpdateAsync_ChangingTypeThatWouldOrphanChildren_IsRejected()
        {
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Feature", Title = "F" },
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Story", ParentId = 1 });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().UpdateAsync(Dev, 1, new SaveWorkItemDto { Title = "F", Type = "Task" }));
        }

        [Fact]
        public async Task UpdateAsync_StatusChangeForbiddenByWorkflow_IsRejected_AndChangesNothing()
        {
            var item = new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Title = "T", Status = "todo" };
            SetTasks(item);
            _workflow.EnsureTransitionAllowedAsync(1, "todo", "completed").Returns(Task.FromException(new InvalidOperationException("workflow")));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().UpdateAsync(Dev, 1, new SaveWorkItemDto { Title = "Renamed", Type = "Task", Status = "completed" }));

            Assert.Equal("T", item.Title);
            Assert.Equal("todo", item.Status);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateAsync_ToCompleted_StampsCompletedAt_AndReopeningClearsIt()
        {
            var item = new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Title = "T", Status = "todo" };
            SetTasks(item);

            await CreateSut().UpdateAsync(Dev, 1, new SaveWorkItemDto { Title = "T", Type = "Task", Status = "completed" });
            Assert.NotNull(item.CompletedAt);

            await CreateSut().UpdateAsync(Dev, 1, new SaveWorkItemDto { Title = "T", Type = "Task", Status = "in-progress" });
            Assert.Null(item.CompletedAt);
        }
    }

    public class ProjectAccessServiceTests
    {
        private readonly IRepository<ProjectUser> _members = Substitute.For<IRepository<ProjectUser>>();
        private readonly IRepository<User> _users = Substitute.For<IRepository<User>>();
        private readonly IRepository<Project> _projects = Substitute.For<IRepository<Project>>();

        private ProjectAccessService CreateSut() => new(_members, _users, _projects);

        private void Setup(bool pmAccess, string? role)
        {
            _users.Query().Returns(new List<User> { new() { Id = 7, IsActive = true, PmAccess = pmAccess } }.BuildMock());
            _members.Query().Returns((role == null
                ? new List<ProjectUser>()
                : new List<ProjectUser> { new() { ProjectId = 1, UserId = 7, ProjectRole = role } }).BuildMock());
            _projects.Query().Returns(new List<Project> { new() { Id = 1 } }.BuildMock());
        }

        [Fact]
        public async Task RequireAsync_NonMember_IsNotFound()
        {
            Setup(pmAccess: true, role: null);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().RequireAsync(new PmCaller(7, false), 1, PmAction.View));
        }

        [Fact]
        public async Task RequireAsync_MemberWithoutModuleAccess_IsNotFound_EvenWithAProjectRole()
        {
            Setup(pmAccess: false, role: ProjectRoles.ProjectAdmin);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().RequireAsync(new PmCaller(7, false), 1, PmAction.View));
        }

        [Fact]
        public async Task RequireAsync_ViewerWritingIsForbidden()
        {
            Setup(pmAccess: true, role: ProjectRoles.Viewer);
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().RequireAsync(new PmCaller(7, false), 1, PmAction.CreateItem));
        }

        [Fact]
        public async Task RequireAsync_DeveloperCanCreate()
        {
            Setup(pmAccess: true, role: ProjectRoles.Developer);
            await CreateSut().RequireAsync(new PmCaller(7, false), 1, PmAction.CreateItem);
        }

        [Fact]
        public async Task RequireAsync_SuperAdminBypassesMembershipAndModuleFlag_ButNotAMissingProject()
        {
            Setup(pmAccess: false, role: null);
            await CreateSut().RequireAsync(new PmCaller(1, true), 1, PmAction.ManageMembers);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateSut().RequireAsync(new PmCaller(1, true), 999, PmAction.View));
        }
    }
}
