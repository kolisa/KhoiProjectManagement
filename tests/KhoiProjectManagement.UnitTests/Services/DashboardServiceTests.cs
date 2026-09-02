using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class DashboardServiceTests
    {
        private readonly IDashboardStatsRepository _statsRepo = Substitute.For<IDashboardStatsRepository>();
        private readonly IRepository<ProjectTask> _taskRepo = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<DashboardStatsSnapshot> _snapshotRepo = Substitute.For<IRepository<DashboardStatsSnapshot>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private DashboardService CreateSut() => new(_statsRepo, _taskRepo, _snapshotRepo, _unitOfWork);

        private void SetCounts(DashboardCountsResult counts) =>
            _statsRepo.GetCountsAsync(Arg.Any<DateTime>()).Returns(counts);

        private static void SetNoSnapshots(IRepository<DashboardStatsSnapshot> snapshotRepo) =>
            snapshotRepo.Query().Returns(new List<DashboardStatsSnapshot>().BuildMock());

        [Fact]
        public async Task GetDashboardStatisticsAsync_ComputesCountsAcrossProjectsAndTasks()
        {
            SetCounts(new DashboardCountsResult
            {
                TotalProjects = 3,
                ActiveProjects = 2,
                TotalTasks = 5,
                CompletedTasks = 2,
                InProgressTasks = 1,
                TodoTasks = 1,
                BlockedTasks = 1,
                OverdueTasks = 2
            });
            SetNoSnapshots(_snapshotRepo);

            var result = await CreateSut().GetDashboardStatisticsAsync();

            Assert.Equal(3, result.TotalProjects);
            Assert.Equal(2, result.ActiveProjects);
            Assert.Equal(5, result.TotalTasks);
            Assert.Equal(2, result.CompletedTasks);
            Assert.Equal(1, result.InProgressTasks);
            Assert.Equal(1, result.TodoTasks);
            Assert.Equal(1, result.BlockedTasks);
            Assert.Equal(2, result.OverdueTasks);
            Assert.Equal(40.0, result.CompletionRate);
        }

        [Fact]
        public async Task GetDashboardStatisticsAsync_WhenThereAreNoTasks_CompletionRateIsZeroNotDivideByZero()
        {
            SetCounts(new DashboardCountsResult());
            SetNoSnapshots(_snapshotRepo);

            var result = await CreateSut().GetDashboardStatisticsAsync();

            Assert.Equal(0, result.TotalTasks);
            Assert.Equal(0.0, result.CompletionRate);
        }

        [Fact]
        public async Task GetDashboardStatisticsAsync_WhenNoSnapshotAtLeastSevenDaysOldExists_DeltasAreNull()
        {
            SetCounts(new DashboardCountsResult());
            // Only a recent snapshot (3 days old) exists - too recent to serve as the 7-day baseline.
            _snapshotRepo.Query().Returns(new List<DashboardStatsSnapshot>
            {
                new() { CapturedAt = DateTime.UtcNow.AddDays(-3), ActiveProjects = 1, TotalTasks = 1, OverdueTasks = 1, CompletionRate = 50 },
            }.BuildMock());

            var result = await CreateSut().GetDashboardStatisticsAsync();

            Assert.Null(result.ActiveProjectsDelta);
            Assert.Null(result.TotalTasksDelta);
            Assert.Null(result.OverdueTasksDelta);
            Assert.Null(result.CompletionRateDelta);
        }

        [Fact]
        public async Task GetDashboardStatisticsAsync_WhenABaselineSnapshotExists_ComputesDeltasAgainstIt()
        {
            SetCounts(new DashboardCountsResult
            {
                TotalProjects = 2,
                ActiveProjects = 2,
                TotalTasks = 2,
                CompletedTasks = 1,
                TodoTasks = 1,
                OverdueTasks = 1
            });
            _snapshotRepo.Query().Returns(new List<DashboardStatsSnapshot>
            {
                new() { CapturedAt = DateTime.UtcNow.AddDays(-10), ActiveProjects = 1, TotalTasks = 1, OverdueTasks = 0, CompletionRate = 100 },
            }.BuildMock());

            var result = await CreateSut().GetDashboardStatisticsAsync();

            // Now: ActiveProjects=2, TotalTasks=2, OverdueTasks=1, CompletionRate=50
            Assert.Equal(1, result.ActiveProjectsDelta); // 2 - 1
            Assert.Equal(1, result.TotalTasksDelta); // 2 - 1
            Assert.Equal(1, result.OverdueTasksDelta); // 1 - 0
            Assert.Equal(-50.0, result.CompletionRateDelta); // 50 - 100
        }

        [Fact]
        public async Task GetDashboardStatisticsAsync_WhenMultipleEligibleSnapshotsExist_UsesTheMostRecentOneAsBaseline()
        {
            SetCounts(new DashboardCountsResult { TotalProjects = 3, ActiveProjects = 3 });
            _snapshotRepo.Query().Returns(new List<DashboardStatsSnapshot>
            {
                // Both are >= 7 days old; the 8-day-old one is the more recent (closer to today) baseline.
                new() { CapturedAt = DateTime.UtcNow.AddDays(-8), ActiveProjects = 2, TotalTasks = 0, OverdueTasks = 0, CompletionRate = 0 },
                new() { CapturedAt = DateTime.UtcNow.AddDays(-30), ActiveProjects = 100, TotalTasks = 0, OverdueTasks = 0, CompletionRate = 0 },
            }.BuildMock());

            var result = await CreateSut().GetDashboardStatisticsAsync();

            Assert.Equal(1, result.ActiveProjectsDelta); // 3 - 2, not 3 - 100
        }

        [Fact]
        public async Task GetWeeklyCompletionAsync_CountsTasksCompletedEachDayOfTheCurrentWeek()
        {
            var monday = CurrentWeekMonday();

            _taskRepo.Query().Returns(new List<ProjectTask>
            {
                new() { Id = 1, Status = "completed", CompletedAt = monday.AddDays(0).AddHours(9) },
                new() { Id = 2, Status = "completed", CompletedAt = monday.AddDays(0).AddHours(15) },
                new() { Id = 3, Status = "completed", CompletedAt = monday.AddDays(3).AddHours(12) },
            }.BuildMock());

            var result = await CreateSut().GetWeeklyCompletionAsync();

            Assert.Equal(7, result.Length);
            Assert.Equal(2, result[0]); // Monday
            Assert.Equal(1, result[3]); // Thursday
            Assert.Equal(0, result[1]);
            Assert.Equal(0, result[6]);
        }

        [Fact]
        public async Task GetWeeklyCompletionAsync_WhenNoTasksCompletedThisWeek_ReturnsAllZeros()
        {
            _taskRepo.Query().Returns(new List<ProjectTask>().BuildMock());

            var result = await CreateSut().GetWeeklyCompletionAsync();

            Assert.Equal(new int[7], result);
        }

        [Fact]
        public async Task GetWeeklyCompletionAsync_ExcludesTasksCompletedBeforeOrAfterTheCurrentWeek()
        {
            var monday = CurrentWeekMonday();
            var nextMonday = monday.AddDays(7);

            _taskRepo.Query().Returns(new List<ProjectTask>
            {
                new() { Id = 1, Status = "completed", CompletedAt = monday.AddSeconds(-1) }, // just before the week starts
                new() { Id = 2, Status = "completed", CompletedAt = nextMonday }, // boundary is exclusive - next week
                new() { Id = 3, Status = "todo", CompletedAt = null },
            }.BuildMock());

            var result = await CreateSut().GetWeeklyCompletionAsync();

            Assert.Equal(new int[7], result);
        }

        [Fact]
        public async Task CaptureSnapshotAsync_AddsASnapshotWithTheCurrentStatisticsAndSaves()
        {
            SetCounts(new DashboardCountsResult
            {
                TotalProjects = 1,
                ActiveProjects = 1,
                TotalTasks = 2,
                CompletedTasks = 1,
                TodoTasks = 1
            });
            SetNoSnapshots(_snapshotRepo);

            await CreateSut().CaptureSnapshotAsync();

            _snapshotRepo.Received(1).Add(Arg.Is<DashboardStatsSnapshot>(s =>
                s.TotalProjects == 1 &&
                s.ActiveProjects == 1 &&
                s.TotalTasks == 2 &&
                s.CompletedTasks == 1 &&
                s.TodoTasks == 1 &&
                s.OverdueTasks == 0 &&
                s.CompletionRate == 50));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        private static DateTime CurrentWeekMonday()
        {
            var today = DateTime.UtcNow.Date;
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            return today.AddDays(-daysSinceMonday);
        }
    }
}
