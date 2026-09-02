using System.Security.Claims;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface ITimesheetService
    {
        // userId omitted defaults to the caller; a different userId requires timesheets.view_all.
        Task<List<TimesheetDto>> GetTimesheetsAsync(int? userId, string? status, ClaimsPrincipal caller);
        Task<TimesheetDto?> GetTimesheetByIdAsync(int id, ClaimsPrincipal caller);
        Task<TimesheetDto> CreateTimesheetAsync(CreateTimesheetDto dto, ClaimsPrincipal caller);
        Task<bool> UpdateTimesheetAsync(int id, UpdateTimesheetDto dto, ClaimsPrincipal caller);
        // Notifies everyone holding finance.manage (in-app + email, opt-out respected) plus - if
        // ccEmails is non-empty - sends the same email to those addresses directly, no in-app
        // notification (they aren't necessarily registered users).
        Task<bool> SubmitTimesheetAsync(int id, List<string> ccEmails, ClaimsPrincipal caller);
        Task<bool> ApproveTimesheetAsync(int id, ClaimsPrincipal caller);
        Task<bool> RejectTimesheetAsync(int id, string reason, ClaimsPrincipal caller);
    }
}
