namespace KhoiProjectManagement.Application
{
    public interface IReportScheduleService
    {
        Task<List<ScheduledReportDto>> GetSchedulesAsync();
        Task<ScheduledReportDto> CreateScheduleAsync(CreateScheduledReportDto dto, int createdByUserId);
        Task<bool> DeleteScheduleAsync(int id);

        Task<List<ReportExportHistoryDto>> GetRecentExportsAsync(int take = 10);

        // Called by ScheduledReportJob - runs every due schedule, generating + persisting an export via
        // IReportExportService and advancing NextRunAt by 7 days.
        Task RunDueSchedulesAsync();
    }
}
