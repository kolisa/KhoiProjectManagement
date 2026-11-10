using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class PmSprintServiceTests
    {
        private static readonly PmCaller Sm = new(10, false);

        private readonly IRepository<Sprint> _sprints = Substitute.For<IRepository<Sprint>>();
        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<TaskStatusHistory> _history = Substitute.For<IRepository<TaskStatusHistory>>();
        private readonly IPmWorkItemService _workItems = Substitute.For<IPmWorkItemService>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
        private readonly IActivityLogService _activity = Substitute.For<IActivityLogService>();
        private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

        public PmSprintServiceTests()
        {
            _workItems.GetAsync(default!, default).ReturnsForAnyArgs(new WorkItemDto());
            _history.Query().Returns(new List<TaskStatusHistory>().BuildMock());
        }

        private PmSprintService CreateSut() => new(_sprints, _tasks, _history, _workItems, _access, _activity, _uow);
        private void SetSprints(params Sprint[] s) => _sprints.Query().Returns(_ => s.ToList().BuildMock());
        private void SetTasks(params ProjectTask[] t) => _tasks.Query().Returns(_ => t.ToList().BuildMock());

        private static Sprint Planned(int id = 1) => new() { Id = id, ProjectId = 1, Name = "S" + id, State = SprintStates.Planned, StartDate = new DateTime(2026, 11, 2), EndDate = new DateTime(2026, 11, 15) };

        [Fact]
        public async Task CreateAsync_WithoutPermission_IsForbidden()
        {
            _access.RequireAsync(Sm, 1, PmAction.ManageSprint).Returns(Task.FromException(new ForbiddenException()));
            await Assert.ThrowsAsync<ForbiddenException>(() => CreateSut().CreateAsync(Sm, 1, new SaveSprintDto { Name = "x" }));
            _sprints.DidNotReceiveWithAnyArgs().Add(default!);
        }

        [Fact]
        public async Task CreateAsync_StartsPlanned_WithTrimmedNameAndDateOnlyBounds()
        {
            Sprint? added = null;
            _sprints.When(r => r.Add(Arg.Any<Sprint>())).Do(ci => { added = ci.Arg<Sprint>(); added.Id = 7; SetSprints(added); });

            await CreateSut().CreateAsync(Sm, 1, new SaveSprintDto
            {
                Name = "  Sprint 1 ", StartDate = new DateTime(2026, 11, 2, 15, 30, 0), EndDate = new DateTime(2026, 11, 15, 9, 0, 0), Capacity = 20,
            });

            Assert.Equal("Sprint 1", added!.Name);
            Assert.Equal(SprintStates.Planned, added.State);
            Assert.Equal(new DateTime(2026, 11, 2), added.StartDate);
            Assert.Equal(20, added.Capacity);
        }

        [Fact]
        public async Task StartAsync_SnapshotsCommittedPoints_AndActivates()
        {
            var sprint = Planned();
            SetSprints(sprint);
            SetTasks(
                new ProjectTask { Id = 1, SprintId = 1, StoryPoints = 5 },
                new ProjectTask { Id = 2, SprintId = 1, StoryPoints = 3 },
                new ProjectTask { Id = 3, SprintId = 1, StoryPoints = null });

            await CreateSut().StartAsync(Sm, 1);

            Assert.Equal(SprintStates.Active, sprint.State);
            Assert.Equal(8, sprint.CommittedPoints);
            Assert.NotNull(sprint.StartedAt);
        }

        [Fact]
        public async Task StartAsync_WhenAnotherSprintIsActive_IsRejected()
        {
            var planned = Planned(1);
            SetSprints(planned, new Sprint { Id = 2, ProjectId = 1, State = SprintStates.Active });
            SetTasks();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().StartAsync(Sm, 1));
            Assert.Contains("already has an active sprint", ex.Message);
            Assert.Equal(SprintStates.Planned, planned.State);
        }

        [Fact]
        public async Task StartAsync_OfACompletedSprint_IsRejected()
        {
            SetSprints(new Sprint { Id = 1, ProjectId = 1, State = SprintStates.Completed });
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().StartAsync(Sm, 1));
        }

        [Fact]
        public async Task CompleteAsync_SnapshotsDonePoints_AndSendsUnfinishedWorkBackToTheBacklog()
        {
            var sprint = new Sprint { Id = 1, ProjectId = 1, Name = "S1", State = SprintStates.Active, CommittedPoints = 13 };
            var done = new ProjectTask { Id = 1, SprintId = 1, Status = "completed", StoryPoints = 5 };
            var open = new ProjectTask { Id = 2, SprintId = 1, Status = "in-progress", StoryPoints = 8 };
            SetSprints(sprint);
            SetTasks(done, open);

            await CreateSut().CompleteAsync(Sm, 1, new CompleteSprintDto());

            Assert.Equal(SprintStates.Completed, sprint.State);
            Assert.Equal(5, sprint.CompletedPoints);
            Assert.Equal(1, done.SprintId);
            Assert.Null(open.SprintId);
        }

        [Fact]
        public async Task CompleteAsync_CanCarryUnfinishedWorkIntoAPlannedSprint()
        {
            var active = new Sprint { Id = 1, ProjectId = 1, State = SprintStates.Active };
            var next = Planned(2);
            var open = new ProjectTask { Id = 2, SprintId = 1, Status = "todo", StoryPoints = 3 };
            SetSprints(active, next);
            SetTasks(open);

            await CreateSut().CompleteAsync(Sm, 1, new CompleteSprintDto { MoveIncompleteToSprintId = 2 });

            Assert.Equal(2, open.SprintId);
        }

        [Fact]
        public async Task CompleteAsync_CarryOverToANonPlannedSprint_IsRejected_AndChangesNothing()
        {
            var active = new Sprint { Id = 1, ProjectId = 1, State = SprintStates.Active };
            var open = new ProjectTask { Id = 2, SprintId = 1, Status = "todo" };
            SetSprints(active, new Sprint { Id = 3, ProjectId = 1, State = SprintStates.Completed });
            SetTasks(open);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut().CompleteAsync(Sm, 1, new CompleteSprintDto { MoveIncompleteToSprintId = 3 }));

            Assert.Equal(SprintStates.Active, active.State);
            Assert.Equal(1, open.SprintId);
        }

        [Fact]
        public async Task DeleteAsync_OnlyPlannedSprints_AndItemsReturnToBacklog()
        {
            var planned = Planned();
            var item = new ProjectTask { Id = 1, SprintId = 1 };
            SetSprints(planned);
            SetTasks(item);

            await CreateSut().DeleteAsync(Sm, 1);

            Assert.Null(item.SprintId);
            _sprints.Received(1).Remove(planned);

            SetSprints(new Sprint { Id = 5, ProjectId = 1, State = SprintStates.Active });
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().DeleteAsync(Sm, 5));
        }

        [Fact]
        public async Task AssignAsync_RejectsEpics_CompletedSprints_AndForeignSprints()
        {
            SetSprints(Planned(1), new Sprint { Id = 2, ProjectId = 1, State = SprintStates.Completed }, new Sprint { Id = 3, ProjectId = 99, State = SprintStates.Planned });

            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Epic" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AssignAsync(Sm, 1, 1));

            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Story" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AssignAsync(Sm, 1, 2));
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AssignAsync(Sm, 1, 3));
        }

        [Fact]
        public async Task AssignAsync_MovesAStoryIntoAPlannedSprint_AndBackOut()
        {
            var story = new ProjectTask { Id = 1, ProjectId = 1, Type = "Story", Title = "T" };
            SetSprints(Planned(1));
            SetTasks(story);

            await CreateSut().AssignAsync(Sm, 1, 1);
            Assert.Equal(1, story.SprintId);

            await CreateSut().AssignAsync(Sm, 1, null);
            Assert.Null(story.SprintId);
        }

        [Fact]
        public async Task AssignAsync_ItemInACompletedSprint_CannotBeMoved()
        {
            var done = new Sprint { Id = 2, ProjectId = 1, State = SprintStates.Completed };
            SetSprints(done, Planned(1));
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", SprintId = 2, Sprint = done });

            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().AssignAsync(Sm, 1, 1));
        }

        [Fact]
        public async Task GetBurndownAsync_PlotsRemainingPointsFromStatusHistory()
        {
            var start = DateTime.UtcNow.Date.AddDays(-2);
            var sprint = new Sprint
            {
                Id = 1, ProjectId = 1, State = SprintStates.Active, StartDate = start, EndDate = start.AddDays(4), CommittedPoints = 10,
            };
            SetSprints(sprint);
            SetTasks(
                new ProjectTask { Id = 1, SprintId = 1, StoryPoints = 4 },
                new ProjectTask { Id = 2, SprintId = 1, StoryPoints = 6 });
            _history.Query().Returns(new List<TaskStatusHistory>
            {
                new() { TaskId = 1, FromStatus = "todo", ToStatus = "completed", ChangedAt = start.AddDays(1).AddHours(10) },
                new() { TaskId = 2, FromStatus = "todo", ToStatus = "completed", ChangedAt = start.AddDays(2).AddHours(9) },
                new() { TaskId = 2, FromStatus = "completed", ToStatus = "in-progress", ChangedAt = start.AddDays(2).AddHours(12) },
            }.BuildMock());

            var result = await CreateSut().GetBurndownAsync(Sm, 1);

            Assert.Equal(10, result.CommittedPoints);
            Assert.Equal(5, result.Points.Count);
            Assert.Equal(new int?[] { 10, 6, 6, null, null }, result.Points.Select(p => p.Remaining));
            Assert.Equal(10, result.Points[0].Ideal);
            Assert.Equal(0, result.Points[4].Ideal);
        }

        [Fact]
        public async Task GetVelocityAsync_ReturnsCompletedSprintsOldestFirst_WithAverage()
        {
            SetSprints(
                new Sprint { Id = 1, ProjectId = 1, Name = "A", State = SprintStates.Completed, CommittedPoints = 10, CompletedPoints = 8, CompletedAt = new DateTime(2026, 1, 1) },
                new Sprint { Id = 2, ProjectId = 1, Name = "B", State = SprintStates.Completed, CommittedPoints = 12, CompletedPoints = 12, CompletedAt = new DateTime(2026, 2, 1) },
                new Sprint { Id = 3, ProjectId = 1, Name = "C", State = SprintStates.Active, CommittedPoints = 9 });

            var result = await CreateSut().GetVelocityAsync(Sm, 1, 6);

            Assert.Equal(new[] { "A", "B" }, result.Sprints.Select(s => s.Name));
            Assert.Equal(10, result.AverageCompleted);
        }
    }
}
