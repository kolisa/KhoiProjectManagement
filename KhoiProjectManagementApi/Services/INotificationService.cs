using KhoiProjectManagement.Models;

namespace KhoiProjectManagementApi.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(int userId, string type, string message, int? taskId = null, int? projectId = null);
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(int userId);
        Task MarkAsReadAsync(int notificationId);
        Task CheckOverdueTasksAsync();
    }
}
