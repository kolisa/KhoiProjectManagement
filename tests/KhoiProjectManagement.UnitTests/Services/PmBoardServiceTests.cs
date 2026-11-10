using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class PmBoardServiceTests
    {
        private static readonly PmCaller Dev = new(10, false);

        private readonly IRepository<BoardColumn> _columns = Substitute.For<IRepository<BoardColumn>>();
        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<TaskStatusHistory> _history = Substitute.For<IRepository<TaskStatusHistory>>();
        private readonly IPmWorkItemService _workItems = Substitute.For<IPmWorkItemService>();
        private readonly IPmWorkflowService _workflow = Substitute.For<IPmWorkflowService>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly IActivityLogService _activity = Substitute.For<IActivityLogService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        public PmBoardServiceTests()
        {
            _workItems.GetAsync(default!, default).ReturnsForAnyArgs(new WorkItemDto());
        }

        private PmBoardService CreateSut() => new(_columns, _tasks, _history, _workItems, _workflow, _access, _activity, _uow);

        private void SetColumns(int? wipInProgress = null) =>
            _columns.Query().Returns(new List<BoardColumn>
            {
                new() { Id = 1, ProjectId = 1, Status = "todo", Name = "To do", Position = 0 },
                new() { Id = 2, ProjectId = 1, Status = "in-progress", Name = "Doing", Position = 1, WipLimit = wipInProgress },
                new() { Id = 3, ProjectId = 1, Status = "blocked", Name = "Blocked", Position = 2 },
                new() { Id = 4, ProjectId = 1, Status = "completed", Name = "Done", Position = 3 },
            }.BuildMock());

        private void SetTasks(params ProjectTask[] items) => _tasks.Query().Returns(_ => items.ToList().BuildMock());

        [Fact]
        public async Task GetBoardAsync_WithNoColumns_SeedsTheFourDefaults()
        {
            _columns.Query().Returns(new List<BoardColumn>().BuildMock());
            _workItems.GetBacklogAsync(default!, default, default!).ReturnsForAnyArgs(new List<WorkItemDto>());

            var board = await CreateSut().GetBoardAsync(Dev, 1);

            _columns.Received(1).AddRange(Arg.Is<IEnumerable<BoardColumn>>(c => c.Count() == 4));
            Assert.Equal(new[] { "todo", "in-progress", "blocked", "completed" }, board.Columns.Select(c => c.Status));
        }

        [Fact]
        public async Task GetBoardAsync_ExcludesEpicsAndFeatures_AndGroupsByStatus()
        {
            SetColumns(wipInProgress: 1);
            _workItems.GetBacklogAsync(default!, default, default!).ReturnsForAnyArgs(new List<WorkItemDto>
            {
                new() { Id = 1, Type = "Epic", Status = "todo" },
                new() { Id = 2, Type = "Feature", Status = "in-progress" },
                new() { Id = 3, Type = "Story", Status = "in-progress" },
                new() { Id = 4, Type = "Bug", Status = "in-progress" },
                new() { Id = 5, Type = "Task", Status = "completed" },
            });

            var board = await CreateSut().GetBoardAsync(Dev, 1);

            var doing = board.Columns.Single(c => c.Status == "in-progress");
            Assert.Equal(new[] { 3, 4 }, doing.Items.Select(i => i.Id));
            Assert.True(doing.OverWip);
            Assert.Empty(board.Columns.Single(c => c.Status == "todo").Items);
        }

        [Fact]
        public async Task MoveAsync_IntoAFullColumn_IsRejected_AndChangesNothing()
        {
            SetColumns(wipInProgress: 1);
            var card = new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Status = "todo" };
            SetTasks(card, new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Status = "in-progress" });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().MoveAsync(Dev, 1, new MoveWorkItemDto { Status = "in-progress" }));

            Assert.Contains("WIP limit", ex.Message);
            Assert.Equal("todo", card.Status);
            _history.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task MoveAsync_ForbiddenByTheProjectWorkflow_IsRejected_AndChangesNothing()
        {
            SetColumns();
            var card = new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Status = "todo" };
            SetTasks(card);
            _workflow.EnsureTransitionAllowedAsync(1, "todo", "completed").Returns(Task.FromException(new InvalidOperationException("workflow")));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().MoveAsync(Dev, 1, new MoveWorkItemDto { Status = "completed" }));

            Assert.Equal("todo", card.Status);
            _history.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task MoveAsync_ReorderingInsideAFullColumn_IsAllowed()
        {
            SetColumns(wipInProgress: 1);
            var card = new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Status = "in-progress", Rank = 5000 };
            SetTasks(card, new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Status = "in-progress", Rank = 1000 });

            await CreateSut().MoveAsync(Dev, 1, new MoveWorkItemDto { Status = "in-progress", AfterItemId = null });

            Assert.Equal(0, card.Rank);
            _history.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task MoveAsync_ToInProgress_RecordsHistory_StampsStartedAt_AndLogsActivity()
        {
            SetColumns();
            var card = new ProjectTask { Id = 1, ProjectId = 1, Type = "Story", Title = "S", Status = "todo" };
            SetTasks(card);

            await CreateSut().MoveAsync(Dev, 1, new MoveWorkItemDto { Status = "in-progress" });

            Assert.Equal("in-progress", card.Status);
            Assert.NotNull(card.StartedAt);
            _history.Received(1).Add(Arg.Is<TaskStatusHistory>(h => h.FromStatus == "todo" && h.ToStatus == "in-progress" && h.ChangedById == Dev.UserId));
            await _activity.Received(1).LogAsync("WorkItem", card.Id, "S", Dev.UserId, "moved", "todo -> in-progress");
        }

        [Fact]
        public async Task MoveAsync_ToDone_SetsCompletedAt_AndLeavingDoneClearsIt()
        {
            SetColumns();
            var card = new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Status = "in-progress" };
            SetTasks(card);

            await CreateSut().MoveAsync(Dev, 1, new MoveWorkItemDto { Status = "completed" });
            Assert.NotNull(card.CompletedAt);

            await CreateSut().MoveAsync(Dev, 1, new MoveWorkItemDto { Status = "in-progress" });
            Assert.Null(card.CompletedAt);
        }

        [Fact]
        public async Task MoveAsync_OfAnEpic_IsRejected()
        {
            SetColumns();
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Epic", Status = "todo" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().MoveAsync(Dev, 1, new MoveWorkItemDto { Status = "in-progress" }));
        }

        [Fact]
        public async Task MoveAsync_WithoutEditPermission_IsForbidden()
        {
            SetColumns();
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Status = "todo" });
            _access.RequireAsync(Dev, 1, PmAction.EditItem).Returns(Task.FromException(new ForbiddenException()));

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                CreateSut().MoveAsync(Dev, 1, new MoveWorkItemDto { Status = "in-progress" }));
        }

        [Fact]
        public async Task SaveColumnsAsync_RequiresExactlyOneColumnPerStatus()
        {
            SetColumns();
            var missing = new List<SaveBoardColumnDto>
            {
                new() { Status = "todo", Name = "A" }, new() { Status = "todo", Name = "B" },
                new() { Status = "blocked", Name = "C" }, new() { Status = "completed", Name = "D" },
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().SaveColumnsAsync(Dev, 1, missing));
        }

        [Fact]
        public async Task SaveColumnsAsync_AppliesNamesOrderAndWipLimits()
        {
            SetColumns();
            _workItems.GetBacklogAsync(default!, default, default!).ReturnsForAnyArgs(new List<WorkItemDto>());
            var cols = new List<SaveBoardColumnDto>
            {
                new() { Status = "completed", Name = " Shipped ", WipLimit = null },
                new() { Status = "in-progress", Name = "Doing", WipLimit = 3 },
                new() { Status = "todo", Name = "Ready" },
                new() { Status = "blocked", Name = "Stuck" },
            };

            var board = await CreateSut().SaveColumnsAsync(Dev, 1, cols);

            Assert.Equal(new[] { "completed", "in-progress", "todo", "blocked" }, board.Columns.OrderBy(c => c.Position).Select(c => c.Status));
            Assert.Equal("Shipped", board.Columns.Single(c => c.Status == "completed").Name);
            Assert.Equal(3, board.Columns.Single(c => c.Status == "in-progress").WipLimit);
        }
    }
}
