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

        // Reminds anyone still MustChangePassword after Notifications:LoginReminderThresholdDays -
        // covers both "never logged in at all" and "logged in once with the temp password but never
        // finished resetting it," since MustChangePassword only clears on a completed reset either way.
        Task CheckInactiveUsersAsync();

        // Sends each fully-onboarded active user a rolling-7-day summary of their task/project/Library
        // activity. Deduped via a WeeklyDigest notification within Notifications:WeeklyDigestRepeatDays,
        // since Quartz's in-memory job store can't be trusted alone to prevent duplicate sends across
        // frequent redeploys (see WeeklyDigestJob wiring in Program.cs).
        Task GenerateWeeklyDigestsAsync();

        // Nudges users who have never created a LibraryFile or uploaded a LibraryFileVersion, once
        // they're past Notifications:NoDocumentsThresholdDays since account creation. Deduped via
        // Notifications:NoDocumentsRepeatDays.
        Task CheckUsersWithNoDocumentsAsync();

        // Distinct population from CheckInactiveUsersAsync: users who *did* finish onboarding
        // (MustChangePassword == false) but haven't logged in for Notifications:DormantUserThresholdDays.
        // Deduped via Notifications:DormantUserRepeatDays.
        Task CheckDormantUsersAsync();

        // Sends a happy-birthday email to every active user whose DateOfBirth's month/day matches
        // today. Deduped by a same-day BirthdayGreeting notification so a boot-time trigger alongside
        // the daily schedule can't double-send.
        Task CheckBirthdaysAsync();

        // Defaults to true (opt-out model) when no preference row exists for this (userId, type) pair.
        Task<bool> IsEmailEnabledAsync(int userId, string notificationType);
        Task<List<NotificationPreferenceDto>> GetPreferencesAsync(int userId);
        Task SetPreferencesAsync(int userId, List<UpdateNotificationPreferenceDto> updates);
    }
}
