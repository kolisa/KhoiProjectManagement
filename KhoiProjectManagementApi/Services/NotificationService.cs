using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ProjectManagementContext _context;
        private readonly IEmailService _emailService;

        public NotificationService(ProjectManagementContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task CreateNotificationAsync(int userId, string type, string message, int? taskId = null, int? projectId = null, int? wikiPageId = null, int? ideaId = null)
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
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsEmailEnabledAsync(int userId, string notificationType)
        {
            var preference = await _context.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == notificationType);

            return preference?.EmailEnabled ?? true;
        }

        public async Task<List<NotificationPreferenceDto>> GetPreferencesAsync(int userId)
        {
            var overrides = await _context.NotificationPreferences
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

                var existing = await _context.NotificationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == update.NotificationType);

                if (existing == null)
                {
                    _context.NotificationPreferences.Add(new NotificationPreference
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

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .Include(n => n.Task)
                .Include(n => n.Project)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task CheckOverdueTasksAsync()
        {
            var overdueTasks = await _context.Tasks
                .Include(t => t.AssignedTo)
                .Include(t => t.Project)
                .Where(t => t.AssignedToId != null &&
                           t.Status != "completed" &&
                           t.DueDate < DateTime.UtcNow)
                .ToListAsync();

            foreach (var task in overdueTasks)
            {
                // Check if we already sent an overdue notification in the last 24 hours
                var existingNotification = await _context.Notifications
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
