using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class SystemOverviewEmailSettingsServiceTests
    {
        private readonly IRepository<SystemOverviewEmailSettings> _settingsRepo = Substitute.For<IRepository<SystemOverviewEmailSettings>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IJobRescheduler _jobRescheduler = Substitute.For<IJobRescheduler>();

        private SystemOverviewEmailSettingsService CreateSut() => new(_settingsRepo, _unitOfWork, _jobRescheduler);

        private static SystemOverviewEmailSettings SeedRow(User? updatedBy = null) => new()
        {
            Id = 1,
            Enabled = true,
            DayOfWeek = DayOfWeek.Friday,
            Hour = 10,
            Minute = 0,
            UpdatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedByUser = updatedBy
        };

        [Fact]
        public async Task GetAsync_ReturnsTheSingleRowIncludingTheUpdatedByUsersName()
        {
            var row = SeedRow(new User { Id = 5, Name = "Kolisa Mjobo" });
            _settingsRepo.Query().Returns(new List<SystemOverviewEmailSettings> { row }.BuildMock());

            var dto = await CreateSut().GetAsync();

            Assert.True(dto.Enabled);
            Assert.Equal(DayOfWeek.Friday, dto.DayOfWeek);
            Assert.Equal(10, dto.Hour);
            Assert.Equal(0, dto.Minute);
            Assert.Equal("Kolisa Mjobo", dto.UpdatedByUserName);
        }

        [Fact]
        public async Task UpdateAsync_AppliesToTheLiveSchedulerBeforePersisting()
        {
            var row = SeedRow();
            _settingsRepo.Query().Returns(new List<SystemOverviewEmailSettings> { row }.BuildMock());

            var dto = new UpdateSystemOverviewEmailSettingsDto { Enabled = true, DayOfWeek = DayOfWeek.Monday, Hour = 9, Minute = 30 };
            await CreateSut().UpdateAsync(dto, updatedByUserId: 7);

            await _jobRescheduler.Received(1).ApplySystemOverviewEmailScheduleAsync(true, DayOfWeek.Monday, 9, 30);
            Assert.Equal(DayOfWeek.Monday, row.DayOfWeek);
            Assert.Equal(9, row.Hour);
            Assert.Equal(30, row.Minute);
            Assert.Equal(7, row.UpdatedByUserId);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateAsync_WhenTheReschedulerThrows_DoesNotPersistTheChange()
        {
            var row = SeedRow();
            _settingsRepo.Query().Returns(new List<SystemOverviewEmailSettings> { row }.BuildMock());
            _jobRescheduler.ApplySystemOverviewEmailScheduleAsync(Arg.Any<bool>(), Arg.Any<DayOfWeek>(), Arg.Any<int>(), Arg.Any<int>())
                .Returns(Task.FromException(new InvalidOperationException("scheduler unavailable")));

            var dto = new UpdateSystemOverviewEmailSettingsDto { Enabled = true, DayOfWeek = DayOfWeek.Monday, Hour = 9, Minute = 30 };
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().UpdateAsync(dto, updatedByUserId: 7));

            Assert.Equal(DayOfWeek.Friday, row.DayOfWeek); // unchanged
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }
    }
}
