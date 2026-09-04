using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class SystemOverviewEmailSettingsService : ISystemOverviewEmailSettingsService
    {
        private readonly IRepository<SystemOverviewEmailSettings> _settingsRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJobRescheduler _jobRescheduler;

        public SystemOverviewEmailSettingsService(
            IRepository<SystemOverviewEmailSettings> settingsRepo,
            IUnitOfWork unitOfWork,
            IJobRescheduler jobRescheduler)
        {
            _settingsRepo = settingsRepo;
            _unitOfWork = unitOfWork;
            _jobRescheduler = jobRescheduler;
        }

        // Single-row table (seeded with Id=1 - see ProjectManagementContext.OnModelCreating) - there is
        // never more than one row, so no filter/id parameter is needed here.
        public async Task<SystemOverviewEmailSettingsDto> GetAsync()
        {
            var settings = await _settingsRepo.Query().Include(s => s.UpdatedByUser).FirstAsync();
            return ToDto(settings);
        }

        public async Task<SystemOverviewEmailSettingsDto> UpdateAsync(UpdateSystemOverviewEmailSettingsDto dto, int updatedByUserId)
        {
            var settings = await _settingsRepo.Query().FirstAsync();

            // Apply to the live scheduler first - if this ever throws, nothing is persisted and the
            // previous schedule keeps running untouched.
            await _jobRescheduler.ApplySystemOverviewEmailScheduleAsync(dto.Enabled, dto.DayOfWeek, dto.Hour, dto.Minute);

            settings.Enabled = dto.Enabled;
            settings.DayOfWeek = dto.DayOfWeek;
            settings.Hour = dto.Hour;
            settings.Minute = dto.Minute;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            settings.UpdatedByUserId = updatedByUserId;
            await _unitOfWork.SaveChangesAsync();

            return await GetAsync();
        }

        private static SystemOverviewEmailSettingsDto ToDto(SystemOverviewEmailSettings settings) => new()
        {
            Enabled = settings.Enabled,
            DayOfWeek = settings.DayOfWeek,
            Hour = settings.Hour,
            Minute = settings.Minute,
            UpdatedAtUtc = settings.UpdatedAtUtc,
            UpdatedByUserName = settings.UpdatedByUser?.Name
        };
    }
}
