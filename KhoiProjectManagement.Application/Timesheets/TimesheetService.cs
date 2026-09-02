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
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<UserRole> _userRoleRepo;
        private readonly IRepository<RolePermission> _rolePermissionRepo;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;

        public TimesheetService(
            IRepository<Timesheet> timesheetRepo,
            IRepository<TimesheetEntry> entryRepo,
            IRepository<User> userRepo,
            IRepository<UserRole> userRoleRepo,
            IRepository<RolePermission> rolePermissionRepo,
            INotificationService notificationService,
            IEmailService emailService,
            IUnitOfWork unitOfWork)
        {
            _timesheetRepo = timesheetRepo;
            _entryRepo = entryRepo;
            _userRepo = userRepo;
            _userRoleRepo = userRoleRepo;
            _rolePermissionRepo = rolePermissionRepo;
            _notificationService = notificationService;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<TimesheetDto>> GetTimesheetsAsync(int? userId, string? status, ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            var canViewAll = caller.HasClaim("permission", "timesheets.view_all") || caller.HasClaim("permission", "timesheets.approve");

            if (userId.HasValue && userId.Value != callerId && !canViewAll)
                throw new UnauthorizedAccessException("Caller lacks timesheets.view_all access to another user's timesheets.");

            var query = _timesheetRepo.Query()
                .Include(t => t.User)
                .Include(t => t.Approver)
                .Include(t => t.Entries).ThenInclude(e => e.Project)
                .AsQueryable();

            // userId explicitly given -> exactly that user's timesheets (already permission-checked
            // above). userId omitted: a caller who can view_all/approve gets everyone's (this is what
            // the Dashboard's "Pending Timesheets" widget and the Approvals view both actually need -
            // "show me what's out there to approve", not just my own) - anyone else still only ever
            // sees their own, the same as before this method understood the "list everyone" case at all.
            if (userId.HasValue)
                query = query.Where(t => t.UserId == userId.Value);
            else if (!canViewAll)
                query = query.Where(t => t.UserId == callerId);

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

        public async Task<bool> SubmitTimesheetAsync(int id, List<string> ccEmails, ClaimsPrincipal caller)
        {
            // Entries/User needed for the notification below (hours total, submitter's display name) -
            // not needed for the status-transition logic itself, but cheaper to Include once here than
            // to reload the timesheet a second time just to notify.
            var timesheet = await _timesheetRepo.Query()
                .Include(t => t.User)
                .Include(t => t.Entries)
                .FirstOrDefaultAsync(t => t.Id == id);
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

            await NotifyFinanceAsync(timesheet, ccEmails);

            return true;
        }

        // Notifies every active user holding finance.manage (in-app notification + email, opt-out
        // respected via IsEmailEnabledAsync - same convention as every other notification type), plus
        // sends the same email straight to any explicit ccEmails with no in-app notification, since
        // those addresses aren't necessarily registered users at all (e.g. an external accountant).
        private async Task NotifyFinanceAsync(Timesheet timesheet, List<string> ccEmails)
        {
            var submitterName = timesheet.User?.Name ?? "A team member";
            var totalHours = timesheet.Entries.Sum(e => e.Hours);

            var financeManagerRoleIds = await _rolePermissionRepo.Query()
                .Where(rp => rp.Permission.Name == "finance.manage")
                .Select(rp => rp.RoleId)
                .ToListAsync();

            var financeManagerUserIds = await _userRoleRepo.Query()
                .Where(ur => financeManagerRoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            var financeManagers = await _userRepo.Query()
                .Where(u => financeManagerUserIds.Contains(u.Id) && u.IsActive)
                .ToListAsync();

            foreach (var manager in financeManagers)
            {
                await _notificationService.CreateNotificationAsync(
                    manager.Id,
                    NotificationTypes.TimesheetSubmitted,
                    $"{submitterName} submitted a timesheet for {timesheet.PeriodStart:MMM d} - {timesheet.PeriodEnd:MMM d} ({totalHours}h)."
                );

                if (await _notificationService.IsEmailEnabledAsync(manager.Id, NotificationTypes.TimesheetSubmitted))
                {
                    try
                    {
                        await _emailService.SendTimesheetSubmittedEmailAsync(manager.Email, submitterName, timesheet.PeriodStart, timesheet.PeriodEnd, totalHours);
                    }
                    catch
                    {
                        // The in-app notification already saved - a failed send must not stop the loop.
                    }
                }
            }

            foreach (var ccEmail in ccEmails.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    await _emailService.SendTimesheetSubmittedEmailAsync(ccEmail, submitterName, timesheet.PeriodStart, timesheet.PeriodEnd, totalHours);
                }
                catch
                {
                    // Same reasoning - one bad CC address shouldn't block the rest.
                }
            }
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
