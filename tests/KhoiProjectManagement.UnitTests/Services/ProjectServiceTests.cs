using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class ProjectServiceTests
    {
        private readonly IRepository<Project> _projectRepo = Substitute.For<IRepository<Project>>();
        private readonly IRepository<ProjectUser> _projectUserRepo = Substitute.For<IRepository<ProjectUser>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<ProjectTag> _projectTagRepo = Substitute.For<IRepository<ProjectTag>>();
        private readonly IRepository<Tag> _tagRepo = Substitute.For<IRepository<Tag>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
        private readonly ISpaceService _spaceService = Substitute.For<ISpaceService>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly IActivityLogService _activityLogService = Substitute.For<IActivityLogService>();
        private readonly IAppTransaction _transaction = Substitute.For<IAppTransaction>();

        public ProjectServiceTests()
        {
            _unitOfWork.BeginTransactionAsync().Returns(_transaction);
            _tagRepo.Query().Returns(new List<Tag>().BuildMock());
        }

        private ProjectService CreateSut() => new(
            _projectRepo, _projectUserRepo, _userRepo, _projectTagRepo, _tagRepo,
            _unitOfWork, _notificationService, _spaceService, _emailService, _activityLogService);

        [Fact]
        public async Task CreateProjectAsync_WhenNoTeamMembersOrTags_CommitsWithoutTouchingSpaceOrNotifications()
        {
            Project? added = null;
            _projectRepo.When(r => r.Add(Arg.Any<Project>())).Do(ci =>
            {
                added = ci.Arg<Project>();
                added.Id = 100;
            });
            _projectRepo.Query().Returns(_ => new List<Project> { added! }.BuildMock());

            var dto = new CreateProjectDto { Name = "New Project", Description = "desc", Priority = "medium" };

            var sut = CreateSut();
            var result = await sut.CreateProjectAsync(dto, 1);

            Assert.Equal("New Project", result.Name);
            Assert.Empty(result.TeamMembers);
            await _transaction.Received(1).CommitAsync();
            await _transaction.DidNotReceive().RollbackAsync();
            await _spaceService.DidNotReceiveWithAnyArgs().EnsureProjectSpaceAsync(default, default);
            await _notificationService.DidNotReceiveWithAnyArgs().CreateNotificationAsync(default, default!, default!);
        }

        [Fact]
        public async Task CreateProjectAsync_WhenTeamMembersGiven_SyncsProjectSpaceAndNotifiesEachMember()
        {
            Project? added = null;
            _projectRepo.When(r => r.Add(Arg.Any<Project>())).Do(ci =>
            {
                added = ci.Arg<Project>();
                added.Id = 200;
            });
            _projectRepo.Query().Returns(_ => new List<Project> { added! }.BuildMock());
            _spaceService.EnsureProjectSpaceAsync(200, Arg.Any<int>()).Returns(55);
            _notificationService.IsEmailEnabledAsync(Arg.Any<int>(), Arg.Any<string>()).Returns(false);

            var dto = new CreateProjectDto { Name = "Team Project", Priority = "high", TeamMemberIds = new List<int> { 1, 2 } };

            var sut = CreateSut();
            await sut.CreateProjectAsync(dto, 1);

            await _spaceService.Received(1).SyncSpaceMembersAsync(55, dto.TeamMemberIds, PermissionLevel.Write, Arg.Any<int>());
            await _notificationService.Received(1).CreateNotificationAsync(1, "project_created", Arg.Any<string>(), null, 200, null, null, null);
            await _notificationService.Received(1).CreateNotificationAsync(2, "project_created", Arg.Any<string>(), null, 200, null, null, null);
        }

        [Fact]
        public async Task CreateProjectAsync_WhenDownstreamStepThrows_RollsBackTransactionAndRethrows()
        {
            _projectRepo.When(r => r.Add(Arg.Any<Project>())).Do(ci => ci.Arg<Project>().Id = 300);
            _spaceService.EnsureProjectSpaceAsync(Arg.Any<int>(), Arg.Any<int>())
                .Returns(Task.FromException<int>(new InvalidOperationException("space creation failed")));

            var dto = new CreateProjectDto { Name = "Doomed Project", Priority = "low", TeamMemberIds = new List<int> { 1 } };

            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateProjectAsync(dto, 1));
            await _transaction.Received(1).RollbackAsync();
            await _transaction.DidNotReceive().CommitAsync();
        }

        [Fact]
        public async Task UpdateProjectAsync_WhenProjectDoesNotExist_ReturnsFalse()
        {
            _projectRepo.Query().Returns(new List<Project>().BuildMock());

            var sut = CreateSut();
            var result = await sut.UpdateProjectAsync(999, new UpdateProjectDto { Name = "x", Priority = "low", Status = "active" });

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateProjectAsync_WhenAllTeamMembersRemoved_ClearsSpaceGrantsWithEmptyList()
        {
            var project = new Project { Id = 5, Name = "Existing", CreatedBy = 1, SpaceId = 77 };
            _projectRepo.Query().Returns(new List<Project> { project }.BuildMock());

            var dto = new UpdateProjectDto { Name = "Existing Renamed", Priority = "low", Status = "active", TeamMemberIds = new List<int>() };

            var sut = CreateSut();
            var result = await sut.UpdateProjectAsync(5, dto);

            Assert.True(result);
            await _spaceService.Received(1).SyncSpaceMembersAsync(77, Arg.Is<IEnumerable<int>>(ids => !ids.Any()), PermissionLevel.Write, 1);
        }

        [Theory]
        [InlineData(0, 0, 0.0)]
        [InlineData(4, 2, 50.0)]
        [InlineData(3, 3, 100.0)]
        public async Task GetProjectStatisticsAsync_ComputesCompletionRate(int totalTasks, int completedTasks, double expectedRate)
        {
            var tasks = new List<ProjectTask>();
            for (var i = 0; i < completedTasks; i++)
                tasks.Add(new ProjectTask { Status = "completed" });
            for (var i = 0; i < totalTasks - completedTasks; i++)
                tasks.Add(new ProjectTask { Status = "todo" });

            var project = new Project { Id = 1, Name = "Stats Project", Tasks = tasks };
            _projectRepo.Query().Returns(new List<Project> { project }.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetProjectStatisticsAsync(1);

            Assert.NotNull(result);
            Assert.Equal(totalTasks, result!.TotalTasks);
            Assert.Equal(expectedRate, result.CompletionRate);
        }

        [Fact]
        public async Task GetProjectStatisticsAsync_WhenProjectDoesNotExist_ReturnsNull()
        {
            _projectRepo.Query().Returns(new List<Project>().BuildMock());

            var sut = CreateSut();
            var result = await sut.GetProjectStatisticsAsync(999);

            Assert.Null(result);
        }
    }
}
