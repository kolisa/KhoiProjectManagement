using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class NotificationService : INotificationService
    {
        private readonly IRepository<Notification> _notificationRepo;
        private readonly IRepository<NotificationPreference> _preferenceRepo;
        private readonly IRepository<ProjectTask> _taskRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;

        public NotificationService(
            IRepository<Notification> notificationRepo,
            IRepository<NotificationPreference> preferenceRepo,
            IRepository<ProjectTask> taskRepo,
            IUnitOfWork unitOfWork,
            IEmailService emailService)
        {
            _notificationRepo = notificationRepo;
            _preferenceRepo = preferenceRepo;
            _taskRepo = taskRepo;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
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
            return await _notificationRepo.Query()
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
    }
}
