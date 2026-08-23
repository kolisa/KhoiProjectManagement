using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(int userId, string type, string message, int? taskId = null, int? projectId = null, int? wikiPageId = null, int? ideaId = null, int? reminderId = null);
        Task<IEnumerable<Notification>> GetUserNotificationsAsync(int userId);
        Task MarkAsReadAsync(int notificationId);
        Task CheckOverdueTasksAsync();

        // Defaults to true (opt-out model) when no preference row exists for this (userId, type) pair.
        Task<bool> IsEmailEnabledAsync(int userId, string notificationType);
        Task<List<NotificationPreferenceDto>> GetPreferencesAsync(int userId);
        Task SetPreferencesAsync(int userId, List<UpdateNotificationPreferenceDto> updates);
    }
}
