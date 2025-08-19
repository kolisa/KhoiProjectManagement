using KhoiProjectManagement.Models;
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

        public async Task CreateNotificationAsync(int userId, string type, string message, int? taskId = null, int? projectId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Message = message,
                TaskId = taskId,
                ProjectId = projectId,
                IsRead = false
            };

            _context.Notifications.Add(notification);
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

                    await _emailService.SendOverdueTaskEmailAsync(
                        task.AssignedTo.Email,
                        task.Title,
                        task.DueDate
                    );
                }
            }
        }
    }
}
