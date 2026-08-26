using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class ReportScheduleService : IReportScheduleService
    {
        private readonly IRepository<ScheduledReport> _scheduleRepo;
        private readonly IRepository<ReportExportHistory> _historyRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IReportExportService _exportService;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;

        public ReportScheduleService(
            IRepository<ScheduledReport> scheduleRepo,
            IRepository<ReportExportHistory> historyRepo,
            IRepository<User> userRepo,
            IReportExportService exportService,
            IEmailService emailService,
            IUnitOfWork unitOfWork)
        {
            _scheduleRepo = scheduleRepo;
            _historyRepo = historyRepo;
            _userRepo = userRepo;
            _exportService = exportService;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<ScheduledReportDto>> GetSchedulesAsync()
        {
            return await _scheduleRepo.Query()
                .Include(s => s.CreatedByUser)
                .OrderByDescending(s => s.Id)
                .Select(s => new ScheduledReportDto
                {
                    Id = s.Id,
                    ReportType = s.ReportType,
                    Format = s.Format,
                    CreatedByName = s.CreatedByUser.Name,
                    NextRunAt = s.NextRunAt,
                    LastRunAt = s.LastRunAt,
                    IsActive = s.IsActive
                })
                .ToListAsync();
        }

        public async Task<ScheduledReportDto> CreateScheduleAsync(CreateScheduledReportDto dto, int createdByUserId)
        {
            if (!ReportTypes.IsValid(dto.ReportType))
                throw new InvalidOperationException($"Unknown report type '{dto.ReportType}'.");
            if (!ReportFormats.IsValid(dto.Format))
                throw new InvalidOperationException($"Unknown format '{dto.Format}'. Must be one of: {string.Join(", ", ReportFormats.All)}.");

            var schedule = new ScheduledReport
            {
                ReportType = dto.ReportType,
                Format = dto.Format,
                CreatedByUserId = createdByUserId,
                NextRunAt = DateTime.UtcNow.AddDays(7),
                IsActive = true
            };

            _scheduleRepo.Add(schedule);
            await _unitOfWork.SaveChangesAsync();

            var creator = await _userRepo.FindAsync(createdByUserId);

            return new ScheduledReportDto
            {
                Id = schedule.Id,
                ReportType = schedule.ReportType,
                Format = schedule.Format,
                CreatedByName = creator?.Name ?? string.Empty,
                NextRunAt = schedule.NextRunAt,
                LastRunAt = schedule.LastRunAt,
                IsActive = schedule.IsActive
            };
        }

        public async Task<bool> DeleteScheduleAsync(int id)
        {
            var schedule = await _scheduleRepo.FindAsync(id);
            if (schedule == null)
                return false;

            _scheduleRepo.Remove(schedule);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<ReportExportHistoryDto>> GetRecentExportsAsync(int take = 10)
        {
            return await _historyRepo.Query()
                .Include(h => h.GeneratedByUser)
                .OrderByDescending(h => h.GeneratedAt)
                .Take(take)
                .Select(h => new ReportExportHistoryDto
                {
                    Id = h.Id,
                    ReportType = h.ReportType,
                    Format = h.Format,
                    GeneratedByName = h.GeneratedByUser.Name,
                    GeneratedAt = h.GeneratedAt,
                    FileSizeBytes = h.FileSizeBytes
                })
                .ToListAsync();
        }

        public async Task RunDueSchedulesAsync()
        {
            var due = await _scheduleRepo.Query()
                .Include(s => s.CreatedByUser)
                .Where(s => s.IsActive && s.NextRunAt <= DateTime.UtcNow)
                .ToListAsync();

            foreach (var schedule in due)
            {
                var (content, contentType, fileName) = await _exportService.ExportReportAsync(schedule.ReportType, schedule.Format, schedule.CreatedByUserId);

                schedule.LastRunAt = DateTime.UtcNow;
                schedule.NextRunAt = schedule.NextRunAt.AddDays(7);
                await _unitOfWork.SaveChangesAsync();

                try
                {
                    await _emailService.SendScheduledReportEmailAsync(schedule.CreatedByUser.Email, fileName, content, fileName, contentType);
                }
                catch
                {
                    // Export already generated and persisted - a failed send must never block the next
                    // schedule from running or lose the export itself. Already logged to EmailLog.
                }
            }
        }
    }
}
