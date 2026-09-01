using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KhoiProjectManagement.Application
{
    public class NotificationService : INotificationService
    {
        private readonly IRepository<Notification> _notificationRepo;
        private readonly IRepository<NotificationPreference> _preferenceRepo;
        private readonly IRepository<ProjectTask> _taskRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<LibraryFile> _libraryFileRepo;
        private readonly IRepository<LibraryFileVersion> _libraryFileVersionRepo;
        private readonly IRepository<ProjectUser> _projectUserRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public NotificationService(
            IRepository<Notification> notificationRepo,
            IRepository<NotificationPreference> preferenceRepo,
            IRepository<ProjectTask> taskRepo,
            IRepository<User> userRepo,
            IRepository<LibraryFile> libraryFileRepo,
            IRepository<LibraryFileVersion> libraryFileVersionRepo,
            IRepository<ProjectUser> projectUserRepo,
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _notificationRepo = notificationRepo;
            _preferenceRepo = preferenceRepo;
            _taskRepo = taskRepo;
            _userRepo = userRepo;
            _libraryFileRepo = libraryFileRepo;
            _libraryFileVersionRepo = libraryFileVersionRepo;
            _projectUserRepo = projectUserRepo;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task CreateNotificationAsync(int userId, string type, string message, int? taskId = null, int? projectId = null, int? wikiPageId = null, int? ideaId = null, int? reminderId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Message = message,
                TaskId = taskId,
                ProjectId = projectId,
                WikiPageId = wikiPageId,
                IdeaId = ideaId,
                ReminderId = reminderId,
                IsRead = false
            };

            _notificationRepo.Add(notification);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> IsEmailEnabledAsync(int userId, string notificationType)
        {
            var preference = await _preferenceRepo.Query()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == notificationType);

            return preference?.EmailEnabled ?? true;
        }

        public async Task<List<NotificationPreferenceDto>> GetPreferencesAsync(int userId)
        {
            var overrides = await _preferenceRepo.Query()
                .Where(p => p.UserId == userId)
                .ToDictionaryAsync(p => p.NotificationType, p => p.EmailEnabled);

            return NotificationTypes.Catalog.Select(c => new NotificationPreferenceDto
            {
                NotificationType = c.Type,
                DisplayName = c.DisplayName,
                Description = c.Description,
                EmailEnabled = overrides.TryGetValue(c.Type, out var enabled) ? enabled : true
            }).ToList();
        }

        public async Task SetPreferencesAsync(int userId, List<UpdateNotificationPreferenceDto> updates)
        {
            foreach (var update in updates)
            {
                if (!NotificationTypes.IsValid(update.NotificationType))
                    throw new InvalidOperationException($"Unknown notification type '{update.NotificationType}'.");

                var existing = await _preferenceRepo.Query()
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == update.NotificationType);

                if (existing == null)
                {
                    _preferenceRepo.Add(new NotificationPreference
                    {
                        UserId = userId,
                        NotificationType = update.NotificationType,
                        EmailEnabled = update.EmailEnabled
                    });
                }
                else
                {
                    existing.EmailEnabled = update.EmailEnabled;
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(int userId)
        {
            // AsNoTracking is load-bearing, not just a perf tweak: with change tracking on, loading a
            // notification's Task and a (different notification's) Project in the same query lets EF's
            // automatic relationship fixup wire Task.Project <-> Project.Tasks behind the scenes
            // whenever their FKs happen to match - neither navigation was Included that deep, but
            // Project.Tasks is a change-tracked collection initialized to `new List<ProjectTask>()`
            // (see Project.cs), so EF still populates it once both entities are tracked. That produces
            // a real object cycle (Notification -> Task -> Project -> Tasks -> [that Task] -> ...) which
            // System.Text.Json has no ReferenceHandler configured to tolerate, 500ing this endpoint
            // outright. No-tracking queries never perform fixup, so this is the actual fix, not a
            // side effect of "it's read-only anyway."
            return await _notificationRepo.Query()
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .Include(n => n.Task)
                .Include(n => n.Project)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _notificationRepo.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task CheckOverdueTasksAsync()
        {
            var overdueTasks = await _taskRepo.Query()
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Where(t => t.AssignedToId != null &&
                           t.Status != "completed" &&
                           t.DueDate < DateTime.UtcNow)
                .ToListAsync();

            foreach (var task in overdueTasks)
            {
                // Check if we already sent an overdue notification in the last 24 hours
                var existingNotification = await _notificationRepo.Query()
                    .Where(n => n.TaskId == task.Id &&
                               n.Type == "overdue" &&
                               n.CreatedAt > DateTime.UtcNow.AddDays(-1))
                    .FirstOrDefaultAsync();

                if (existingNotification == null && task.AssignedTo != null)
                {
                    await CreateNotificationAsync(
                        task.AssignedToId!.Value,
                        "overdue",
                        $"Task '{task.Title}' is overdue (Due: {task.DueDate:yyyy-MM-dd})",
                        taskId: task.Id
                    );

                    if (await IsEmailEnabledAsync(task.AssignedToId.Value, NotificationTypes.Overdue))
                    {
                        try
                        {
                            await _emailService.SendOverdueTaskEmailAsync(
                                task.AssignedTo.Email,
                                task.Title,
                                task.DueDate
                            );
                        }
                        catch
                        {
                            // The in-app notification already saved - a failed SMTP send must not stop
                            // the rest of the overdue-check loop. Already logged to EmailLog.
                        }
                    }
                }
            }
        }

        public async Task CheckInactiveUsersAsync()
        {
            var thresholdDays = int.Parse(_configuration["Notifications:LoginReminderThresholdDays"] ?? "3");
            var repeatDays = int.Parse(_configuration["Notifications:LoginReminderRepeatDays"] ?? "7");
            var cutoff = DateTime.UtcNow.AddDays(-thresholdDays);

            // MustChangePassword alone (not LastLoginAt) is the right single signal - it stays true
            // whether the person never attempted a login at all, or logged in once with the temp
            // password and abandoned setup before choosing their own. Both are "hasn't finished joining."
            var pendingUsers = await _userRepo.Query()
                .Where(u => u.IsActive && u.MustChangePassword && u.CreatedAt < cutoff)
                .ToListAsync();

            foreach (var user in pendingUsers)
            {
                var recentlyReminded = await _notificationRepo.Query()
                    .Where(n => n.UserId == user.Id &&
                               n.Type == NotificationTypes.LoginReminder &&
                               n.CreatedAt > DateTime.UtcNow.AddDays(-repeatDays))
                    .FirstOrDefaultAsync();

                if (recentlyReminded != null)
                    continue;

                var daysSinceInvite = (int)(DateTime.UtcNow - user.CreatedAt).TotalDays;

                await CreateNotificationAsync(
                    user.Id,
                    NotificationTypes.LoginReminder,
                    $"You haven't finished setting up your account ({daysSinceInvite} days since it was created)."
                );

                if (await IsEmailEnabledAsync(user.Id, NotificationTypes.LoginReminder))
                {
                    try
                    {
                        await _emailService.SendLoginReminderEmailAsync(user.Email, user.Name, daysSinceInvite);
                    }
                    catch
                    {
                        // The in-app notification already saved - a failed send must not stop the loop.
                    }
                }
            }
        }

        public async Task GenerateWeeklyDigestsAsync()
        {
            var repeatDays = int.Parse(_configuration["Notifications:WeeklyDigestRepeatDays"] ?? "6");
            var weekStart = DateTime.UtcNow.AddDays(-7);
            var weekEnd = DateTime.UtcNow;

            // Only fully-onboarded active users - someone still stuck on their temp password has
            // nothing to log into yet, and is already covered by CheckInactiveUsersAsync instead.
            var users = await _userRepo.Query()
                .Where(u => u.IsActive && !u.MustChangePassword)
                .ToListAsync();

            foreach (var user in users)
            {
                var recentlySent = await _notificationRepo.Query()
                    .Where(n => n.UserId == user.Id &&
                               n.Type == NotificationTypes.WeeklyDigest &&
                               n.CreatedAt > DateTime.UtcNow.AddDays(-repeatDays))
                    .FirstOrDefaultAsync();

                if (recentlySent != null)
                    continue;

                var tasksCompleted = await _taskRepo.Query()
                    .CountAsync(t => t.AssignedToId == user.Id && t.CompletedAt != null && t.CompletedAt >= weekStart && t.CompletedAt <= weekEnd);

                var tasksOpen = await _taskRepo.Query()
                    .CountAsync(t => t.AssignedToId == user.Id && t.Status != "completed");

                var tasksOverdue = await _taskRepo.Query()
                    .CountAsync(t => t.AssignedToId == user.Id && t.Status != "completed" && t.DueDate < DateTime.UtcNow);

                var projectsActive = await _projectUserRepo.Query()
                    .Include(pu => pu.Project)
                    .CountAsync(pu => pu.UserId == user.Id && pu.Project.Status == "active");

                var libraryUploads = await _libraryFileVersionRepo.Query()
                    .CountAsync(v => v.UploadedBy == user.Id && v.UploadedAt >= weekStart && v.UploadedAt <= weekEnd);

                await CreateNotificationAsync(
                    user.Id,
                    NotificationTypes.WeeklyDigest,
                    $"Your weekly digest: {tasksCompleted} task(s) completed, {tasksOpen} open, {libraryUploads} Library upload(s)."
                );

                if (await IsEmailEnabledAsync(user.Id, NotificationTypes.WeeklyDigest))
                {
                    try
                    {
                        await _emailService.SendWeeklyDigestEmailAsync(
                            user.Email, user.Name, tasksCompleted, tasksOpen, tasksOverdue, projectsActive, libraryUploads, weekStart, weekEnd);
                    }
                    catch
                    {
                        // The in-app notification already saved - a failed send must not stop the loop.
                    }
                }
            }
        }

        public async Task CheckUsersWithNoDocumentsAsync()
        {
            var thresholdDays = int.Parse(_configuration["Notifications:NoDocumentsThresholdDays"] ?? "14");
            var repeatDays = int.Parse(_configuration["Notifications:NoDocumentsRepeatDays"] ?? "30");
            var cutoff = DateTime.UtcNow.AddDays(-thresholdDays);

            var candidates = await _userRepo.Query()
                .Where(u => u.IsActive && !u.MustChangePassword && u.CreatedAt < cutoff)
                .ToListAsync();

            foreach (var user in candidates)
            {
                // "Uploaded nothing, ever" - checked against both the file-creation FK and the
                // per-version upload FK, since a later version can be uploaded by someone other than
                // the file's original creator (see LibraryFile/LibraryFileVersion).
                var hasCreatedFile = await _libraryFileRepo.Query().AnyAsync(f => f.CreatedBy == user.Id);
                if (hasCreatedFile)
                    continue;

                var hasUploadedVersion = await _libraryFileVersionRepo.Query().AnyAsync(v => v.UploadedBy == user.Id);
                if (hasUploadedVersion)
                    continue;

                var recentlyNudged = await _notificationRepo.Query()
                    .Where(n => n.UserId == user.Id &&
                               n.Type == NotificationTypes.NoDocumentsNudge &&
                               n.CreatedAt > DateTime.UtcNow.AddDays(-repeatDays))
                    .FirstOrDefaultAsync();

                if (recentlyNudged != null)
                    continue;

                await CreateNotificationAsync(
                    user.Id,
                    NotificationTypes.NoDocumentsNudge,
                    "You haven't uploaded any files to the Library yet."
                );

                if (await IsEmailEnabledAsync(user.Id, NotificationTypes.NoDocumentsNudge))
                {
                    try
                    {
                        await _emailService.SendNoDocumentsNudgeEmailAsync(user.Email, user.Name);
                    }
                    catch
                    {
                        // The in-app notification already saved - a failed send must not stop the loop.
                    }
                }
            }
        }

        public async Task CheckDormantUsersAsync()
        {
            var thresholdDays = int.Parse(_configuration["Notifications:DormantUserThresholdDays"] ?? "21");
            var repeatDays = int.Parse(_configuration["Notifications:DormantUserRepeatDays"] ?? "14");
            var cutoff = DateTime.UtcNow.AddDays(-thresholdDays);

            // Distinct from CheckInactiveUsersAsync's MustChangePassword population - this is people
            // who finished onboarding and genuinely stopped logging in.
            var dormantUsers = await _userRepo.Query()
                .Where(u => u.IsActive && !u.MustChangePassword && u.LastLoginAt != null && u.LastLoginAt < cutoff)
                .ToListAsync();

            foreach (var user in dormantUsers)
            {
                var recentlyNudged = await _notificationRepo.Query()
                    .Where(n => n.UserId == user.Id &&
                               n.Type == NotificationTypes.DormantUserNudge &&
                               n.CreatedAt > DateTime.UtcNow.AddDays(-repeatDays))
                    .FirstOrDefaultAsync();

                if (recentlyNudged != null)
                    continue;

                var daysSinceLastLogin = (int)(DateTime.UtcNow - user.LastLoginAt!.Value).TotalDays;

                await CreateNotificationAsync(
                    user.Id,
                    NotificationTypes.DormantUserNudge,
                    $"You haven't logged in for {daysSinceLastLogin} days."
                );

                if (await IsEmailEnabledAsync(user.Id, NotificationTypes.DormantUserNudge))
                {
                    try
                    {
                        await _emailService.SendDormantUserNudgeEmailAsync(user.Email, user.Name, daysSinceLastLogin);
                    }
                    catch
                    {
                        // The in-app notification already saved - a failed send must not stop the loop.
                    }
                }
            }
        }

        public async Task CheckBirthdaysAsync()
        {
            var today = DateTime.UtcNow.Date;
            // Feb 29 clamps to Feb 28 in a non-leap year - same rule CalendarService.SafeDate applies
            // when computing the Calendar feed's birthday entries, kept consistent here so someone
            // born on a leap day still gets greeted every year, not just once every four.
            var isFeb28InNonLeapYear = today.Month == 2 && today.Day == 28 && !DateTime.IsLeapYear(today.Year);

            var birthdayUsers = await _userRepo.Query()
                .Where(u => u.IsActive && u.DateOfBirth != null &&
                    (u.DateOfBirth.Value.Month == today.Month && u.DateOfBirth.Value.Day == today.Day ||
                     isFeb28InNonLeapYear && u.DateOfBirth.Value.Month == 2 && u.DateOfBirth.Value.Day == 29))
                .ToListAsync();

            foreach (var user in birthdayUsers)
            {
                var alreadyGreetedToday = await _notificationRepo.Query()
                    .Where(n => n.UserId == user.Id &&
                               n.Type == NotificationTypes.BirthdayGreeting &&
                               n.CreatedAt >= today)
                    .FirstOrDefaultAsync();

                if (alreadyGreetedToday != null)
                    continue;

                await CreateNotificationAsync(
                    user.Id,
                    NotificationTypes.BirthdayGreeting,
                    "Happy birthday! 🎉"
                );

                if (await IsEmailEnabledAsync(user.Id, NotificationTypes.BirthdayGreeting))
                {
                    try
                    {
                        await _emailService.SendBirthdayEmailAsync(user.Email, user.Name);
                    }
                    catch
                    {
                        // The in-app notification already saved - a failed send must not stop the loop.
                    }
                }
            }
        }
    }
}
