using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class ReminderService : IReminderService
    {
        private static readonly string[] ValidPriorities = { "low", "medium", "high" };
        private static readonly string[] ValidRecurrenceTypes = { "Daily", "Weekly", "Monthly" };
        private const string ViewAllPermission = "reminders.view_all";
        private const string ManagePermission = "reminders.manage";

        private readonly IRepository<Reminder> _reminderRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Notification> _notificationRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;

        public ReminderService(
            IRepository<Reminder> reminderRepo,
            IRepository<User> userRepo,
            IRepository<Notification> notificationRepo,
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IEmailService emailService)
        {
            _reminderRepo = reminderRepo;
            _userRepo = userRepo;
            _notificationRepo = notificationRepo;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _emailService = emailService;
        }

        public async Task<List<ReminderDto>> GetRemindersAsync(ReminderFilterDto filter, ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            var canViewAll = caller.HasClaim("permission", ViewAllPermission);

            var query = _reminderRepo.Query()
                .Include(r => r.AssignedTo)
                .Include(r => r.Creator)
                .Include(r => r.RelatedProject)
                .AsQueryable();

            if (!canViewAll)
                query = query.Where(r => r.AssignedToId == callerId || r.CreatedBy == callerId);

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                query = query.Where(r => r.Title.Contains(term) || (r.Description != null && r.Description.Contains(term)));
            }

            if (!string.IsNullOrEmpty(filter.Status))
                query = query.Where(r => r.Status == filter.Status);

            if (!string.IsNullOrEmpty(filter.Priority))
                query = query.Where(r => r.Priority == filter.Priority);

            if (!string.IsNullOrEmpty(filter.Category))
                query = query.Where(r => r.Category == filter.Category);

            if (filter.DueFrom.HasValue)
                query = query.Where(r => r.DueAt >= filter.DueFrom.Value);

            if (filter.DueTo.HasValue)
                query = query.Where(r => r.DueAt <= filter.DueTo.Value);

            if (filter.AssignedToId.HasValue)
                query = query.Where(r => r.AssignedToId == filter.AssignedToId.Value);

            if (filter.CreatedBy.HasValue)
                query = query.Where(r => r.CreatedBy == filter.CreatedBy.Value);

            if (filter.HasRecurrence.HasValue)
                query = filter.HasRecurrence.Value
                    ? query.Where(r => r.RecurrenceType != null)
                    : query.Where(r => r.RecurrenceType == null);

            if (filter.View == "completed")
                query = query.Where(r => r.Status == "Completed");
            else if (filter.View is "today" or "upcoming" or "overdue")
                query = query.Where(r => r.Status != "Completed");

            // EffectiveDue is a private C# method - EF Core can't translate a call to it into SQL, so
            // everything that depends on it (the remaining View filters, the final sort) has to happen
            // after materializing. Reminder lists are inherently small-per-caller (personal, not a huge
            // audit log), so filtering/sorting the rest in memory here is the pragmatic tradeoff - the
            // exact same pattern GetSummaryCountsAsync already uses for the same reason.
            var now = DateTime.UtcNow;
            var todayEnd = now.Date.AddDays(1);
            var materialized = await query.ToListAsync();

            IEnumerable<Reminder> filtered = filter.View switch
            {
                "today" => materialized.Where(r => EffectiveDue(r) >= now.Date && EffectiveDue(r) < todayEnd),
                "upcoming" => materialized.Where(r => EffectiveDue(r) >= todayEnd),
                "overdue" => materialized.Where(r => EffectiveDue(r) < now),
                _ => materialized
            };

            var reminders = filtered.OrderBy(r => r.Status == "Completed").ThenBy(EffectiveDue).ToList();
            return reminders.Select(MapToDto).ToList();
        }

        public async Task<ReminderSummaryCountsDto> GetSummaryCountsAsync(ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            var canViewAll = caller.HasClaim("permission", ViewAllPermission);

            var query = _reminderRepo.Query().AsQueryable();
            if (!canViewAll)
                query = query.Where(r => r.AssignedToId == callerId || r.CreatedBy == callerId);

            var all = await query.ToListAsync();
            var now = DateTime.UtcNow;
            var todayEnd = now.Date.AddDays(1);
            var active = all.Where(r => r.Status != "Completed").ToList();

            return new ReminderSummaryCountsDto
            {
                TotalActive = active.Count,
                DueToday = active.Count(r => EffectiveDue(r) >= now.Date && EffectiveDue(r) < todayEnd),
                Upcoming = active.Count(r => EffectiveDue(r) >= todayEnd),
                Overdue = active.Count(r => EffectiveDue(r) < now),
                Completed = all.Count(r => r.Status == "Completed"),
                HighPriority = active.Count(r => r.Priority == "high"),
            };
        }

        public async Task<ReminderDto?> GetReminderByIdAsync(int id, ClaimsPrincipal caller)
        {
            var reminder = await LoadAsync(id);
            if (reminder == null)
                return null;

            RequireAccess(reminder, caller);
            return MapToDto(reminder);
        }

        public async Task<ReminderDto> CreateReminderAsync(CreateReminderDto dto, ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            var assignedToId = dto.AssignedToId ?? callerId;

            if (assignedToId != callerId && !caller.HasClaim("permission", ManagePermission))
                throw new UnauthorizedAccessException("Caller lacks reminders.manage access to assign a reminder to another user.");

            ValidateRecurrence(dto.RecurrenceType, dto.DueAt, dto.RecurrenceEndDate);

            var reminder = new Reminder
            {
                Title = dto.Title,
                Description = dto.Description,
                DueAt = dto.DueAt,
                Priority = dto.Priority,
                Category = dto.Category,
                AssignedToId = assignedToId,
                CreatedBy = callerId,
                Channel = dto.Channel,
                RecurrenceType = dto.RecurrenceType,
                RecurrenceEndDate = dto.RecurrenceEndDate,
                RecurrenceMaxOccurrences = dto.RecurrenceMaxOccurrences,
                RelatedProjectId = dto.RelatedProjectId
            };

            _reminderRepo.Add(reminder);
            await _unitOfWork.SaveChangesAsync();

            var saved = await LoadAsync(reminder.Id);
            return MapToDto(saved!);
        }

        public async Task<bool> UpdateReminderAsync(int id, UpdateReminderDto dto, ClaimsPrincipal caller)
        {
            var reminder = await _reminderRepo.Query().FirstOrDefaultAsync(r => r.Id == id);
            if (reminder == null)
                return false;

            RequireAccess(reminder, caller);

            var callerId = GetUserId(caller);
            var newAssignedToId = dto.AssignedToId ?? reminder.AssignedToId;
            if (newAssignedToId != reminder.AssignedToId && !caller.HasClaim("permission", ManagePermission))
                throw new UnauthorizedAccessException("Caller lacks reminders.manage access to reassign this reminder.");

            ValidateRecurrence(dto.RecurrenceType, dto.DueAt, dto.RecurrenceEndDate);

            reminder.Title = dto.Title;
            reminder.Description = dto.Description;
            reminder.DueAt = dto.DueAt;
            reminder.Priority = dto.Priority;
            reminder.Category = dto.Category;
            reminder.AssignedToId = newAssignedToId;
            reminder.Channel = dto.Channel;
            reminder.RecurrenceType = dto.RecurrenceType;
            reminder.RecurrenceEndDate = dto.RecurrenceEndDate;
            reminder.RecurrenceMaxOccurrences = dto.RecurrenceMaxOccurrences;
            reminder.RelatedProjectId = dto.RelatedProjectId;
            reminder.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteReminderAsync(int id, ClaimsPrincipal caller)
        {
            var reminder = await _reminderRepo.Query().FirstOrDefaultAsync(r => r.Id == id);
            if (reminder == null)
                return false;

            RequireAccess(reminder, caller);
            _reminderRepo.Remove(reminder);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CompleteAsync(int id, ClaimsPrincipal caller)
        {
            var reminder = await _reminderRepo.Query().FirstOrDefaultAsync(r => r.Id == id);
            if (reminder == null)
                return false;

            RequireAccess(reminder, caller);

            reminder.Status = "Completed";
            reminder.CompletedAt = DateTime.UtcNow;
            reminder.UpdatedAt = DateTime.UtcNow;

            if (reminder.RecurrenceType != null)
                await CreateNextOccurrenceAsync(reminder);

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReopenAsync(int id, ClaimsPrincipal caller)
        {
            var reminder = await _reminderRepo.Query().FirstOrDefaultAsync(r => r.Id == id);
            if (reminder == null)
                return false;

            RequireAccess(reminder, caller);

            reminder.Status = "Pending";
            reminder.CompletedAt = null;
            reminder.SnoozedUntil = null;
            reminder.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SnoozeAsync(int id, SnoozeReminderDto dto, ClaimsPrincipal caller)
        {
            var reminder = await _reminderRepo.Query().FirstOrDefaultAsync(r => r.Id == id);
            if (reminder == null)
                return false;

            RequireAccess(reminder, caller);

            reminder.Status = "Snoozed";
            reminder.SnoozedUntil = dto.SnoozeUntil;
            reminder.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<ReminderDto> DuplicateAsync(int id, ClaimsPrincipal caller)
        {
            var reminder = await _reminderRepo.Query().FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new InvalidOperationException($"Reminder {id} not found.");

            RequireAccess(reminder, caller);

            var copy = new Reminder
            {
                Title = reminder.Title,
                Description = reminder.Description,
                DueAt = reminder.DueAt,
                Priority = reminder.Priority,
                Category = reminder.Category,
                AssignedToId = reminder.AssignedToId,
                CreatedBy = GetUserId(caller),
                Channel = reminder.Channel,
                RecurrenceType = reminder.RecurrenceType,
                RecurrenceEndDate = reminder.RecurrenceEndDate,
                RecurrenceMaxOccurrences = reminder.RecurrenceMaxOccurrences,
                RelatedProjectId = reminder.RelatedProjectId
            };

            _reminderRepo.Add(copy);
            await _unitOfWork.SaveChangesAsync();

            var saved = await LoadAsync(copy.Id);
            return MapToDto(saved!);
        }

        public async Task<int> BulkCompleteAsync(BulkReminderActionDto dto, ClaimsPrincipal caller)
        {
            var reminders = await LoadAccessibleAsync(dto.Ids, caller);
            foreach (var reminder in reminders)
            {
                reminder.Status = "Completed";
                reminder.CompletedAt = DateTime.UtcNow;
                reminder.UpdatedAt = DateTime.UtcNow;
                if (reminder.RecurrenceType != null)
                    await CreateNextOccurrenceAsync(reminder);
            }
            await _unitOfWork.SaveChangesAsync();
            return reminders.Count;
        }

        public async Task<int> BulkDeleteAsync(BulkReminderActionDto dto, ClaimsPrincipal caller)
        {
            var reminders = await LoadAccessibleAsync(dto.Ids, caller);
            _reminderRepo.RemoveRange(reminders);
            await _unitOfWork.SaveChangesAsync();
            return reminders.Count;
        }

        public async Task<int> BulkRescheduleAsync(BulkRescheduleReminderDto dto, ClaimsPrincipal caller)
        {
            var reminders = await LoadAccessibleAsync(dto.Ids, caller);
            foreach (var reminder in reminders)
            {
                reminder.DueAt = dto.DueAt;
                reminder.UpdatedAt = DateTime.UtcNow;
            }
            await _unitOfWork.SaveChangesAsync();
            return reminders.Count;
        }

        public async Task<int> BulkPriorityAsync(BulkPriorityReminderDto dto, ClaimsPrincipal caller)
        {
            if (!ValidPriorities.Contains(dto.Priority))
                throw new InvalidOperationException($"Priority must be one of: {string.Join(", ", ValidPriorities)}");

            var reminders = await LoadAccessibleAsync(dto.Ids, caller);
            foreach (var reminder in reminders)
            {
                reminder.Priority = dto.Priority;
                reminder.UpdatedAt = DateTime.UtcNow;
            }
            await _unitOfWork.SaveChangesAsync();
            return reminders.Count;
        }

        // reminders.manage-gated at the controller - every targeted reminder is reassigned regardless
        // of current owner, unlike the other bulk actions which only ever touch reminders the caller
        // already has access to.
        public async Task<int> BulkAssignAsync(BulkAssignReminderDto dto, ClaimsPrincipal caller)
        {
            var reminders = await _reminderRepo.Query().Where(r => dto.Ids.Contains(r.Id)).ToListAsync();
            foreach (var reminder in reminders)
            {
                reminder.AssignedToId = dto.AssignedToId;
                reminder.UpdatedAt = DateTime.UtcNow;
            }
            await _unitOfWork.SaveChangesAsync();
            return reminders.Count;
        }

        public async Task CheckDueRemindersAsync()
        {
            var now = DateTime.UtcNow;
            var dueReminders = await _reminderRepo.Query()
                .Include(r => r.AssignedTo)
                .Where(r => (r.Status == "Pending" && r.DueAt <= now) || (r.Status == "Snoozed" && r.SnoozedUntil <= now))
                .ToListAsync();

            foreach (var reminder in dueReminders)
            {
                // Dedup window matches CheckOverdueTasksAsync's exact 24h pattern - don't re-notify for
                // the same reminder on every hourly run once it's already been flagged today.
                var alreadyNotified = await _notificationRepo.Query()
                    .AnyAsync(n => n.ReminderId == reminder.Id &&
                                   n.Type == NotificationTypes.ReminderDue &&
                                   n.CreatedAt > DateTime.UtcNow.AddDays(-1));
                if (alreadyNotified)
                    continue;

                await _notificationService.CreateNotificationAsync(
                    reminder.AssignedToId,
                    NotificationTypes.ReminderDue,
                    $"Reminder due: {reminder.Title}",
                    reminderId: reminder.Id
                );

                if ((reminder.Channel == "Email" || reminder.Channel == "Both") &&
                    await _notificationService.IsEmailEnabledAsync(reminder.AssignedToId, NotificationTypes.ReminderDue))
                {
                    try
                    {
                        await _emailService.SendReminderDueEmailAsync(reminder.AssignedTo.Email, reminder.Title, reminder.DueAt);
                    }
                    catch
                    {
                        // The in-app notification already saved - a failed SMTP send must not stop the
                        // rest of the due-reminder check loop. Already logged to EmailLog.
                    }
                }
            }
        }

        private static DateTime EffectiveDue(Reminder r) => r.Status == "Snoozed" && r.SnoozedUntil.HasValue ? r.SnoozedUntil.Value : r.DueAt;

        private async Task CreateNextOccurrenceAsync(Reminder original)
        {
            var nextDue = original.RecurrenceType switch
            {
                "Daily" => original.DueAt.AddDays(1),
                "Weekly" => original.DueAt.AddDays(7),
                "Monthly" => original.DueAt.AddMonths(1),
                _ => (DateTime?)null
            };
            if (nextDue == null)
                return;

            if (original.RecurrenceEndDate.HasValue && nextDue.Value > original.RecurrenceEndDate.Value)
                return;

            if (original.RecurrenceMaxOccurrences.HasValue)
            {
                var rootId = original.RecurrenceParentId ?? original.Id;
                var occurrenceCount = await _reminderRepo.Query()
                    .CountAsync(r => r.Id == rootId || r.RecurrenceParentId == rootId);
                if (occurrenceCount >= original.RecurrenceMaxOccurrences.Value)
                    return;
            }

            _reminderRepo.Add(new Reminder
            {
                Title = original.Title,
                Description = original.Description,
                DueAt = nextDue.Value,
                Priority = original.Priority,
                Category = original.Category,
                AssignedToId = original.AssignedToId,
                CreatedBy = original.CreatedBy,
                Channel = original.Channel,
                RecurrenceType = original.RecurrenceType,
                RecurrenceEndDate = original.RecurrenceEndDate,
                RecurrenceMaxOccurrences = original.RecurrenceMaxOccurrences,
                RecurrenceParentId = original.RecurrenceParentId ?? original.Id,
                RelatedProjectId = original.RelatedProjectId
            });
        }

        private void ValidateRecurrence(string? recurrenceType, DateTime dueAt, DateTime? recurrenceEndDate)
        {
            if (recurrenceType == null)
                return;

            if (!ValidRecurrenceTypes.Contains(recurrenceType))
                throw new InvalidOperationException($"RecurrenceType must be one of: {string.Join(", ", ValidRecurrenceTypes)}");

            if (recurrenceEndDate.HasValue && recurrenceEndDate.Value < dueAt)
                throw new InvalidOperationException("RecurrenceEndDate must not be before DueAt.");
        }

        private void RequireAccess(Reminder reminder, ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            if (reminder.AssignedToId != callerId && reminder.CreatedBy != callerId && !caller.HasClaim("permission", ViewAllPermission))
                throw new UnauthorizedAccessException($"Caller lacks access to reminder {reminder.Id}.");
        }

        private async Task<List<Reminder>> LoadAccessibleAsync(List<int> ids, ClaimsPrincipal caller)
        {
            var callerId = GetUserId(caller);
            var canViewAll = caller.HasClaim("permission", ViewAllPermission);

            var reminders = await _reminderRepo.Query().Where(r => ids.Contains(r.Id)).ToListAsync();
            return canViewAll
                ? reminders
                : reminders.Where(r => r.AssignedToId == callerId || r.CreatedBy == callerId).ToList();
        }

        private Task<Reminder?> LoadAsync(int id) =>
            _reminderRepo.Query()
                .Include(r => r.AssignedTo)
                .Include(r => r.Creator)
                .Include(r => r.RelatedProject)
                .FirstOrDefaultAsync(r => r.Id == id);

        private static ReminderDto MapToDto(Reminder r) => new()
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            DueAt = r.DueAt,
            Priority = r.Priority,
            Status = r.Status,
            Category = r.Category,
            AssignedToId = r.AssignedToId,
            AssignedToName = r.AssignedTo?.Name ?? "Unknown",
            CreatedBy = r.CreatedBy,
            CreatedByName = r.Creator?.Name ?? "Unknown",
            Channel = r.Channel,
            SnoozedUntil = r.SnoozedUntil,
            CompletedAt = r.CompletedAt,
            RecurrenceType = r.RecurrenceType,
            RecurrenceEndDate = r.RecurrenceEndDate,
            RecurrenceMaxOccurrences = r.RecurrenceMaxOccurrences,
            RecurrenceParentId = r.RecurrenceParentId,
            RelatedProjectId = r.RelatedProjectId,
            RelatedProjectName = r.RelatedProject?.Name,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt
        };

        private static int GetUserId(ClaimsPrincipal caller)
        {
            var claim = caller.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Caller has no NameIdentifier claim.");
            return int.Parse(claim.Value);
        }
    }
}
