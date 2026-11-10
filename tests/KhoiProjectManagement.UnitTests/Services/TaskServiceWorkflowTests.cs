using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // The classic Tasks tab shares ProjectTask with the PM module, so a project's workflow rules must hold
    // there too - otherwise the rules would only bind people who happen to use the PM screens.
    public class TaskServiceWorkflowTests
    {
        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<TaskStatusHistory> _history = Substitute.For<IRepository<TaskStatusHistory>>();
        private readonly IPmWorkflowService _workflow = Substitute.For<IPmWorkflowService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        private TaskService CreateSut() => new(
            _tasks, Substitute.For<IRepository<User>>(), Substitute.For<IRepository<Project>>(),
            Substitute.For<IRepository<TaskTag>>(), Substitute.For<IRepository<Tag>>(), _uow,
            Substitute.For<INotificationService>(), Substitute.For<IEmailService>(), Substitute.For<IActivityLogService>(),
            _history, _workflow);

        [Fact]
        public async Task UpdateTaskStatusAsync_ForbiddenByTheWorkflow_ChangesNothing()
        {
            var task = new ProjectTask { Id = 1, ProjectId = 7, Status = "todo" };
            _tasks.FindAsync(1).Returns(task);
            _workflow.EnsureTransitionAllowedAsync(7, "todo", "completed").Returns(Task.FromException(new InvalidOperationException("workflow")));

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().UpdateTaskStatusAsync(1, "completed", 10));

            Assert.Equal("todo", task.Status);
            _history.DidNotReceiveWithAnyArgs().Add(default!);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateTaskAsync_ForbiddenByTheWorkflow_RollsBackAndChangesNothing()
        {
            var task = new ProjectTask { Id = 1, ProjectId = 7, Status = "todo", Title = "Old" };
            _tasks.Query().Returns(_ => new List<ProjectTask> { task }.BuildMock());
            _workflow.EnsureTransitionAllowedAsync(7, "todo", "completed").Returns(Task.FromException(new InvalidOperationException("workflow")));

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().UpdateTaskAsync(1, new UpdateTaskDto
            {
                Title = "New", Description = "", Status = "completed", Priority = "medium", Type = "Task", DueDate = DateTime.UtcNow,
            }, 10));

            Assert.Equal("Old", task.Title);
            Assert.Equal("todo", task.Status);
            await _uow.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateTaskStatusAsync_AllowedByTheWorkflow_RecordsTheMove()
        {
            var task = new ProjectTask { Id = 1, ProjectId = 7, Status = "todo" };
            _tasks.FindAsync(1).Returns(task);

            await CreateSut().UpdateTaskStatusAsync(1, "in-progress", 10);

            Assert.Equal("in-progress", task.Status);
            _history.Received(1).Add(Arg.Is<TaskStatusHistory>(h => h.FromStatus == "todo" && h.ToStatus == "in-progress"));
        }
    }
}
