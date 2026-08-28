using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class ReportServiceTests
    {
        private readonly IRepository<Project> _projectRepo = Substitute.For<IRepository<Project>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<ProjectTask> _taskRepo = Substitute.For<IRepository<ProjectTask>>();

        private ReportService CreateSut() => new(_projectRepo, _userRepo, _taskRepo);

        [Fact]
        public async Task GenerateProjectSummaryReportAsync_WhenNoProjects_ReturnsZeroedTotalsWithoutDividingByZero()
        {
            _projectRepo.Query().Returns(new List<Project>().BuildMock());

            var result = await CreateSut().GenerateProjectSummaryReportAsync();

            Assert.Equal(0, result.TotalProjects);
            Assert.Equal(0, result.ActiveProjects);
            Assert.Equal(0, result.OverallCompletionRate);
            Assert.Empty(result.Projects);
        }

        [Fact]
        public async Task GenerateProjectSummaryReportAsync_ComputesPerProjectAndOverallCompletionRates()
        {
            var alpha = new Project
            {
                Id = 1,
                Name = "Alpha",
                Status = "active",
                Tasks = new List<ProjectTask>
                {
                    new() { Status = "completed" },
                    new() { Status = "completed" },
                    new() { Status = "todo" },
                    new() { Status = "in-progress" },
                }
            };
            var beta = new Project
            {
                Id = 2,
                Name = "Beta",
                Status = "completed",
                Tasks = new List<ProjectTask>
                {
                    new() { Status = "completed" },
                    new() { Status = "completed" },
                    new() { Status = "completed" },
                    new() { Status = "completed" },
                }
            };
            var gamma = new Project
            {
                Id = 3,
                Name = "Gamma - no tasks",
                Status = "active",
                Tasks = new List<ProjectTask>()
            };
            _projectRepo.Query().Returns(new List<Project> { alpha, beta, gamma }.BuildMock());

            var result = await CreateSut().GenerateProjectSummaryReportAsync();

            Assert.Equal(3, result.TotalProjects);
            Assert.Equal(2, result.ActiveProjects); // alpha + gamma are "active"; beta is "completed"

            var alphaSummary = result.Projects.Single(p => p.Name == "Alpha");
            Assert.Equal(4, alphaSummary.TasksCount);
            Assert.Equal(2, alphaSummary.CompletedTasks);
            Assert.Equal(50, alphaSummary.CompletionRate);

            var betaSummary = result.Projects.Single(p => p.Name == "Beta");
            Assert.Equal(4, betaSummary.TasksCount);
            Assert.Equal(4, betaSummary.CompletedTasks);
            Assert.Equal(100, betaSummary.CompletionRate);

            var gammaSummary = result.Projects.Single(p => p.Name == "Gamma - no tasks");
            Assert.Equal(0, gammaSummary.TasksCount);
            Assert.Equal(0, gammaSummary.CompletionRate);

            // Overall: 6 completed out of 8 total tasks across all projects. Deliberately chosen as
            // powers of two (unlike the 3-of-5 an intermediate design had) so the expected 75% is an
            // exact double, not just a value that happens to round correctly.
            Assert.Equal(6, result.Projects.Sum(p => p.CompletedTasks));
            Assert.Equal(8, result.Projects.Sum(p => p.TasksCount));
            Assert.Equal(75, result.OverallCompletionRate);
        }

        [Fact]
        public async Task GenerateTeamPerformanceReportAsync_OnlyIncludesActiveUsers()
        {
            var active = new User { Id = 1, Name = "Active User", IsActive = true, AssignedTasks = new List<ProjectTask>() };
            var inactive = new User { Id = 2, Name = "Inactive User", IsActive = false, AssignedTasks = new List<ProjectTask>() };
            _userRepo.Query().Returns(new List<User> { active, inactive }.BuildMock());

            var result = await CreateSut().GenerateTeamPerformanceReportAsync();

            Assert.Single(result.TeamMembers);
            Assert.Equal("Active User", result.TeamMembers[0].Name);
        }

        [Fact]
        public async Task GenerateTeamPerformanceReportAsync_ComputesAssignedCompletedOverdueAndCompletionRate()
        {
            var user = new User
            {
                Id = 1,
                Name = "Jane",
                Position = "Engineer",
                IsActive = true,
                AssignedTasks = new List<ProjectTask>
                {
                    new() { Status = "completed", DueDate = DateTime.UtcNow.AddDays(-30) },
                    new() { Status = "completed", DueDate = DateTime.UtcNow.AddDays(30) },
                    // Not completed and past due => overdue.
                    new() { Status = "todo", DueDate = DateTime.UtcNow.AddDays(-30) },
                    // Not completed but due in the future => not overdue.
                    new() { Status = "in-progress", DueDate = DateTime.UtcNow.AddDays(30) },
                }
            };
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());

            var result = await CreateSut().GenerateTeamPerformanceReportAsync();

            var member = Assert.Single(result.TeamMembers);
            Assert.Equal("Jane", member.Name);
            Assert.Equal("Engineer", member.Position);
            Assert.Equal(4, member.AssignedTasks);
            Assert.Equal(2, member.CompletedTasks);
            Assert.Equal(1, member.OverdueTasks);
            Assert.Equal(50, member.CompletionRate);
        }

        [Fact]
        public async Task GenerateTeamPerformanceReportAsync_WhenUserHasNoAssignedTasks_CompletionRateIsZero()
        {
            var user = new User { Id = 1, Name = "Idle", IsActive = true, AssignedTasks = new List<ProjectTask>() };
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());

            var result = await CreateSut().GenerateTeamPerformanceReportAsync();

            var member = Assert.Single(result.TeamMembers);
            Assert.Equal(0, member.AssignedTasks);
            Assert.Equal(0, member.CompletionRate);
        }

        [Fact]
        public async Task GenerateOverdueTasksReportAsync_ExcludesCompletedAndNotYetDueTasks()
        {
            var project = new Project { Id = 1, Name = "Alpha" };
            var tasks = new List<ProjectTask>
            {
                new() { Id = 1, Title = "Overdue", Status = "todo", DueDate = DateTime.UtcNow.AddDays(-5), Project = project },
                new() { Id = 2, Title = "Completed but late", Status = "completed", DueDate = DateTime.UtcNow.AddDays(-5), Project = project },
                new() { Id = 3, Title = "Not due yet", Status = "todo", DueDate = DateTime.UtcNow.AddDays(5), Project = project },
            };
            _taskRepo.Query().Returns(tasks.BuildMock());

            var result = await CreateSut().GenerateOverdueTasksReportAsync();

            Assert.Equal(1, result.TotalOverdueTasks);
            Assert.Equal("Overdue", Assert.Single(result.Tasks).Title);
        }

        [Fact]
        public async Task GenerateOverdueTasksReportAsync_OrdersByDueDateAscendingAndComputesDaysOverdue()
        {
            var project = new Project { Id = 1, Name = "Alpha" };
            var mostOverdue = new ProjectTask { Id = 1, Title = "Oldest", Status = "todo", DueDate = DateTime.UtcNow.AddDays(-10), Project = project, Priority = "high" };
            var leastOverdue = new ProjectTask { Id = 2, Title = "Newest", Status = "todo", DueDate = DateTime.UtcNow.AddDays(-1), Project = project, Priority = "low" };
            _taskRepo.Query().Returns(new List<ProjectTask> { leastOverdue, mostOverdue }.BuildMock());

            var result = await CreateSut().GenerateOverdueTasksReportAsync();

            Assert.Equal(2, result.TotalOverdueTasks);
            Assert.Equal("Oldest", result.Tasks[0].Title);
            Assert.Equal("Newest", result.Tasks[1].Title);
            Assert.Equal(10, result.Tasks[0].DaysOverdue);
            Assert.Equal("high", result.Tasks[0].Priority);
        }

        [Fact]
        public async Task GenerateOverdueTasksReportAsync_WhenProjectOrAssignedToIsMissing_UsesFallbackLabels()
        {
            var task = new ProjectTask
            {
                Id = 1,
                Title = "Orphan task",
                Status = "todo",
                DueDate = DateTime.UtcNow.AddDays(-1),
                Project = null!,
                AssignedTo = null
            };
            _taskRepo.Query().Returns(new List<ProjectTask> { task }.BuildMock());

            var result = await CreateSut().GenerateOverdueTasksReportAsync();

            var item = Assert.Single(result.Tasks);
            Assert.Equal("Unknown Project", item.Project);
            Assert.Equal("Unassigned", item.AssignedTo);
        }
    }
}
