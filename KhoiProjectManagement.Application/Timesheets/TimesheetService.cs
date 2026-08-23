using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class TimesheetService : ITimesheetService
    {
        private readonly IRepository<Timesheet> _timesheetRepo;
        private readonly IRepository<TimesheetEntry> _entryRepo;
        private readonly IUnitOfWork _unitOfWork;

        public TimesheetService(IRepository<Timesheet> timesheetRepo, IRepository<TimesheetEntry> entryRepo, IUnitOfWork unitOfWork)
        {
            _timesheetRepo = timesheetRepo;
            _entryRepo = entryRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<TimesheetDto>> GetTimesheetsAsync(int? userId, string? status, ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            var targetUserId = userId ?? callerId;

            if (targetUserId != callerId && !caller.HasClaim("permission", "timesheets.view_all"))
                throw new UnauthorizedAccessException("Caller lacks timesheets.view_all access to another user's timesheets.");

            var query = _timesheetRepo.Query()
                .Include(t => t.User)
                .Include(t => t.Approver)
                .Include(t => t.Entries).ThenInclude(e => e.Project)
                .Where(t => t.UserId == targetUserId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status == status);

            var timesheets = await query.OrderByDescending(t => t.PeriodStart).ToListAsync();
            return timesheets.Select(MapToDto).ToList();
        }

        public async Task<TimesheetDto?> GetTimesheetByIdAsync(int id, ClaimsPrincipal caller)
        {
            var timesheet = await LoadAsync(id);
            if (timesheet == null)
                return null;

            var callerId = GetUserId(caller);
            if (timesheet.UserId != callerId && !caller.HasClaim("permission", "timesheets.view_all") && !caller.HasClaim("permission", "timesheets.approve"))
                throw new UnauthorizedAccessException($"Caller lacks access to timesheet {id}.");

            return MapToDto(timesheet);
        }

        public async Task<TimesheetDto> CreateTimesheetAsync(CreateTimesheetDto dto, ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            var timesheet = new Timesheet
            {
                UserId = callerId,
                PeriodStart = dto.PeriodStart,
                PeriodEnd = dto.PeriodEnd,
                Status = "Draft",
                Entries = dto.Entries.Select(MapEntry).ToList()
            };

            _timesheetRepo.Add(timesheet);
            await _unitOfWork.SaveChangesAsync();

            var saved = await LoadAsync(timesheet.Id);
            return MapToDto(saved!);
        }

        public async Task<bool> UpdateTimesheetAsync(int id, UpdateTimesheetDto dto, ClaimsPrincipal caller)
        {
            var timesheet = await _timesheetRepo.Query()
                .Include(t => t.Entries)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (timesheet == null)
                return false;

            var callerId = GetUserId(caller);
            if (timesheet.UserId != callerId)
                throw new UnauthorizedAccessException($"Caller lacks access to modify timesheet {id}.");

            if (timesheet.Status != "Draft" && timesheet.Status != "Rejected")
                throw new InvalidOperationException($"Cannot edit a timesheet with status '{timesheet.Status}'.");

            _entryRepo.RemoveRange(timesheet.Entries);
            timesheet.Entries = dto.Entries.Select(e =>
            {
                var entry = MapEntry(e);
                entry.TimesheetId = timesheet.Id;
                return entry;
            }).ToList();

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SubmitTimesheetAsync(int id, ClaimsPrincipal caller)
        {
            var timesheet = await _timesheetRepo.Query().FirstOrDefaultAsync(t => t.Id == id);
            if (timesheet == null)
                return false;

            var callerId = GetUserId(caller);
            if (timesheet.UserId != callerId)
                throw new UnauthorizedAccessException($"Caller lacks access to submit timesheet {id}.");

            if (timesheet.Status != "Draft" && timesheet.Status != "Rejected")
                throw new InvalidOperationException($"Cannot submit a timesheet with status '{timesheet.Status}'.");

            timesheet.Status = "Submitted";
            timesheet.SubmittedAt = DateTime.UtcNow;
            timesheet.RejectionReason = null;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ApproveTimesheetAsync(int id, ClaimsPrincipal caller)
        {
            var timesheet = await _timesheetRepo.Query().FirstOrDefaultAsync(t => t.Id == id);
            if (timesheet == null)
                return false;

            if (timesheet.Status != "Submitted")
                throw new InvalidOperationException($"Cannot approve a timesheet with status '{timesheet.Status}'.");

            timesheet.Status = "Approved";
            timesheet.ApprovedBy = GetUserId(caller);
            timesheet.ApprovedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectTimesheetAsync(int id, string reason, ClaimsPrincipal caller)
        {
            var timesheet = await _timesheetRepo.Query().FirstOrDefaultAsync(t => t.Id == id);
            if (timesheet == null)
                return false;

            if (timesheet.Status != "Submitted")
                throw new InvalidOperationException($"Cannot reject a timesheet with status '{timesheet.Status}'.");

            timesheet.Status = "Rejected";
            timesheet.RejectionReason = reason;
            timesheet.ApprovedBy = GetUserId(caller);
            timesheet.ApprovedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        private async Task<Timesheet?> LoadAsync(int id)
        {
            return await _timesheetRepo.Query()
                .Include(t => t.User)
                .Include(t => t.Approver)
                .Include(t => t.Entries).ThenInclude(e => e.Project)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        private static TimesheetEntry MapEntry(CreateTimesheetEntryDto dto) => new()
        {
            EntryDate = dto.EntryDate,
            ProjectId = dto.ProjectId,
            Description = dto.Description,
            Hours = dto.Hours
        };

        private static TimesheetDto MapToDto(Timesheet t) => new()
        {
            Id = t.Id,
            UserId = t.UserId,
            UserName = t.User?.Name ?? "Unknown",
            PeriodStart = t.PeriodStart,
            PeriodEnd = t.PeriodEnd,
            Status = t.Status,
            SubmittedAt = t.SubmittedAt,
            ApproverName = t.Approver?.Name,
            ApprovedAt = t.ApprovedAt,
            RejectionReason = t.RejectionReason,
            Entries = t.Entries.Select(e => new TimesheetEntryDto
            {
                Id = e.Id,
                EntryDate = e.EntryDate,
                ProjectId = e.ProjectId,
                ProjectName = e.Project?.Name,
                Description = e.Description,
                Hours = e.Hours
            }).ToList()
        };

        private static int GetUserId(ClaimsPrincipal caller)
        {
            var claim = caller.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Caller has no NameIdentifier claim.");
            return int.Parse(claim.Value);
        }
    }
}
