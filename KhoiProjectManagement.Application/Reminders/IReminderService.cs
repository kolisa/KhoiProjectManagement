using System.Security.Claims;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface IReminderService
    {
        Task<List<ReminderDto>> GetRemindersAsync(ReminderFilterDto filter, ClaimsPrincipal caller);
        Task<ReminderSummaryCountsDto> GetSummaryCountsAsync(ClaimsPrincipal caller);
        Task<ReminderDto?> GetReminderByIdAsync(int id, ClaimsPrincipal caller);
        Task<ReminderDto> CreateReminderAsync(CreateReminderDto dto, ClaimsPrincipal caller);
        Task<bool> UpdateReminderAsync(int id, UpdateReminderDto dto, ClaimsPrincipal caller);
        Task<bool> DeleteReminderAsync(int id, ClaimsPrincipal caller);
        Task<bool> CompleteAsync(int id, ClaimsPrincipal caller);
        Task<bool> ReopenAsync(int id, ClaimsPrincipal caller);
        Task<bool> SnoozeAsync(int id, SnoozeReminderDto dto, ClaimsPrincipal caller);
        Task<ReminderDto> DuplicateAsync(int id, ClaimsPrincipal caller);

        Task<int> BulkCompleteAsync(BulkReminderActionDto dto, ClaimsPrincipal caller);
        Task<int> BulkDeleteAsync(BulkReminderActionDto dto, ClaimsPrincipal caller);
        Task<int> BulkRescheduleAsync(BulkRescheduleReminderDto dto, ClaimsPrincipal caller);
        Task<int> BulkPriorityAsync(BulkPriorityReminderDto dto, ClaimsPrincipal caller);
        Task<int> BulkAssignAsync(BulkAssignReminderDto dto, ClaimsPrincipal caller);

        // Called by ReminderDueCheckerService (hourly hosted service, mirrors
        // NotificationService.CheckOverdueTasksAsync exactly) - not caller-scoped, runs for everyone.
        Task CheckDueRemindersAsync();
    }
}
