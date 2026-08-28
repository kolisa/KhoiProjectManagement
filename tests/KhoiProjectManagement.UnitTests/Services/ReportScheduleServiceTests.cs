using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class ReportScheduleServiceTests
    {
        private readonly IRepository<ScheduledReport> _scheduleRepo = Substitute.For<IRepository<ScheduledReport>>();
        private readonly IRepository<ReportExportHistory> _historyRepo = Substitute.For<IRepository<ReportExportHistory>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IReportExportService _exportService = Substitute.For<IReportExportService>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private ReportScheduleService CreateSut() => new(_scheduleRepo, _historyRepo, _userRepo, _exportService, _emailService, _unitOfWork);

        [Theory]
        [InlineData(ReportTypes.ProjectSummary)]
        [InlineData(ReportTypes.TeamPerformance)]
        [InlineData(ReportTypes.OverdueTasks)]
        public async Task CreateScheduleAsync_WhenReportTypeIsValid_CreatesTheSchedule(string reportType)
        {
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Jane" });

            var result = await CreateSut().CreateScheduleAsync(new CreateScheduledReportDto { ReportType = reportType, Format = ReportFormats.Csv }, createdByUserId: 1);

            Assert.Equal(reportType, result.ReportType);
        }

        [Fact]
        public async Task CreateScheduleAsync_WhenReportTypeIsInvalid_ThrowsAndDoesNotSave()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.CreateScheduleAsync(new CreateScheduledReportDto { ReportType = "NotAReport", Format = ReportFormats.Csv }, createdByUserId: 1));

            _scheduleRepo.DidNotReceive().Add(Arg.Any<ScheduledReport>());
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task CreateScheduleAsync_WhenFormatIsInvalid_ThrowsAndDoesNotSave()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.CreateScheduleAsync(new CreateScheduledReportDto { ReportType = ReportTypes.ProjectSummary, Format = "Xlsx" }, createdByUserId: 1));

            _scheduleRepo.DidNotReceive().Add(Arg.Any<ScheduledReport>());
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task CreateScheduleAsync_SetsNextRunAtSevenDaysOutAndIsActiveTrue()
        {
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Jane" });
            ScheduledReport? added = null;
            _scheduleRepo.When(r => r.Add(Arg.Any<ScheduledReport>())).Do(ci =>
            {
                added = ci.Arg<ScheduledReport>();
                added.Id = 9;
            });

            var before = DateTime.UtcNow;
            var result = await CreateSut().CreateScheduleAsync(new CreateScheduledReportDto { ReportType = ReportTypes.OverdueTasks, Format = ReportFormats.Pdf }, createdByUserId: 1);
            var after = DateTime.UtcNow;

            Assert.NotNull(added);
            Assert.True(added!.IsActive);
            Assert.Null(added.LastRunAt);
            Assert.InRange(added.NextRunAt, before.AddDays(7), after.AddDays(7));
            Assert.Equal(9, result.Id);
            Assert.Equal("Jane", result.CreatedByName);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task CreateScheduleAsync_WhenCreatorLookupReturnsNull_CreatedByNameIsEmpty()
        {
            _userRepo.FindAsync(1).Returns((User?)null);

            var result = await CreateSut().CreateScheduleAsync(new CreateScheduledReportDto { ReportType = ReportTypes.ProjectSummary, Format = ReportFormats.Csv }, createdByUserId: 1);

            Assert.Equal(string.Empty, result.CreatedByName);
        }

        [Fact]
        public async Task DeleteScheduleAsync_WhenScheduleDoesNotExist_ReturnsFalse()
        {
            _scheduleRepo.FindAsync(999).Returns((ScheduledReport?)null);

            var result = await CreateSut().DeleteScheduleAsync(999);

            Assert.False(result);
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteScheduleAsync_WhenScheduleExists_RemovesItAndSaves()
        {
            var schedule = new ScheduledReport { Id = 1, ReportType = ReportTypes.ProjectSummary, Format = ReportFormats.Csv };
            _scheduleRepo.FindAsync(1).Returns(schedule);

            var result = await CreateSut().DeleteScheduleAsync(1);

            Assert.True(result);
            _scheduleRepo.Received(1).Remove(schedule);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetSchedulesAsync_ReturnsSchedulesNewestFirstWithCreatorName()
        {
            var creator = new User { Id = 1, Name = "Jane" };
            var older = new ScheduledReport { Id = 1, ReportType = ReportTypes.ProjectSummary, Format = ReportFormats.Csv, CreatedByUser = creator, IsActive = true };
            var newer = new ScheduledReport { Id = 2, ReportType = ReportTypes.TeamPerformance, Format = ReportFormats.Pdf, CreatedByUser = creator, IsActive = false };
            _scheduleRepo.Query().Returns(new List<ScheduledReport> { older, newer }.BuildMock());

            var result = await CreateSut().GetSchedulesAsync();

            Assert.Equal(new[] { 2, 1 }, result.Select(s => s.Id));
            Assert.Equal("Jane", result[0].CreatedByName);
            Assert.False(result[0].IsActive);
        }

        [Fact]
        public async Task GetRecentExportsAsync_ReturnsMostRecentFirstLimitedByTake()
        {
            var user = new User { Id = 1, Name = "Jane" };
            var exports = Enumerable.Range(1, 15)
                .Select(i => new ReportExportHistory
                {
                    Id = i,
                    ReportType = ReportTypes.ProjectSummary,
                    Format = ReportFormats.Csv,
                    GeneratedByUser = user,
                    GeneratedAt = new DateTime(2026, 1, 1).AddDays(i),
                    FileSizeBytes = 100
                })
                .ToList();
            _historyRepo.Query().Returns(exports.BuildMock());

            var result = await CreateSut().GetRecentExportsAsync(take: 3);

            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { 15, 14, 13 }, result.Select(h => h.Id));
            Assert.Equal("Jane", result[0].GeneratedByName);
        }

        [Fact]
        public async Task RunDueSchedulesAsync_SkipsSchedulesThatAreInactiveOrNotYetDue()
        {
            var creator = new User { Id = 1, Name = "Jane", Email = "jane@khoitech.africa" };
            var dueButInactive = new ScheduledReport { Id = 1, ReportType = ReportTypes.ProjectSummary, Format = ReportFormats.Csv, IsActive = false, NextRunAt = DateTime.UtcNow.AddDays(-1), CreatedByUser = creator, CreatedByUserId = 1 };
            var activeButNotDue = new ScheduledReport { Id = 2, ReportType = ReportTypes.ProjectSummary, Format = ReportFormats.Csv, IsActive = true, NextRunAt = DateTime.UtcNow.AddDays(1), CreatedByUser = creator, CreatedByUserId = 1 };
            _scheduleRepo.Query().Returns(new List<ScheduledReport> { dueButInactive, activeButNotDue }.BuildMock());

            await CreateSut().RunDueSchedulesAsync();

            await _exportService.DidNotReceive().ExportReportAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        }

        [Fact]
        public async Task RunDueSchedulesAsync_ExportsAndAdvancesNextRunAtByOneWeekForEachDueActiveSchedule()
        {
            var creator = new User { Id = 1, Name = "Jane", Email = "jane@khoitech.africa" };
            var originalNextRun = DateTime.UtcNow.AddDays(-1);
            var due = new ScheduledReport
            {
                Id = 1,
                ReportType = ReportTypes.ProjectSummary,
                Format = ReportFormats.Csv,
                IsActive = true,
                NextRunAt = originalNextRun,
                CreatedByUser = creator,
                CreatedByUserId = 1
            };
            _scheduleRepo.Query().Returns(new List<ScheduledReport> { due }.BuildMock());
            _exportService.ExportReportAsync(ReportTypes.ProjectSummary, ReportFormats.Csv, 1)
                .Returns((new byte[] { 1, 2, 3 }, "text/csv", "ProjectSummary_2026-01-01.csv"));

            await CreateSut().RunDueSchedulesAsync();

            Assert.Equal(originalNextRun.AddDays(7), due.NextRunAt);
            Assert.NotNull(due.LastRunAt);
            await _emailService.Received(1).SendScheduledReportEmailAsync(
                "jane@khoitech.africa", "ProjectSummary_2026-01-01.csv", Arg.Any<byte[]>(), "ProjectSummary_2026-01-01.csv", "text/csv");
            await _unitOfWork.Received().SaveChangesAsync();
        }

        [Fact]
        public async Task RunDueSchedulesAsync_WhenSendingTheEmailThrows_StillCompletesWithoutPropagating()
        {
            var creator = new User { Id = 1, Name = "Jane", Email = "jane@khoitech.africa" };
            var due = new ScheduledReport
            {
                Id = 1,
                ReportType = ReportTypes.ProjectSummary,
                Format = ReportFormats.Csv,
                IsActive = true,
                NextRunAt = DateTime.UtcNow.AddDays(-1),
                CreatedByUser = creator,
                CreatedByUserId = 1
            };
            _scheduleRepo.Query().Returns(new List<ScheduledReport> { due }.BuildMock());
            _exportService.ExportReportAsync(ReportTypes.ProjectSummary, ReportFormats.Csv, 1)
                .Returns((new byte[] { 1, 2, 3 }, "text/csv", "file.csv"));
            _emailService.SendScheduledReportEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("SMTP down")));

            var exception = await Record.ExceptionAsync(() => CreateSut().RunDueSchedulesAsync());

            Assert.Null(exception);
        }
    }
}
