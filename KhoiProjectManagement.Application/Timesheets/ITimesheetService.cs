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
        Task<bool> SubmitTimesheetAsync(int id, ClaimsPrincipal caller);
        Task<bool> ApproveTimesheetAsync(int id, ClaimsPrincipal caller);
        Task<bool> RejectTimesheetAsync(int id, string reason, ClaimsPrincipal caller);
    }
}
