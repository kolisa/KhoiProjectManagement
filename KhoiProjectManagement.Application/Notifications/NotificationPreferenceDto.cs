namespace KhoiProjectManagement.Application
{
    public class NotificationPreferenceDto
    {
        public string NotificationType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool EmailEnabled { get; set; }
    }

    public class UpdateNotificationPreferenceDto
    {
        public string NotificationType { get; set; } = string.Empty;
        public bool EmailEnabled { get; set; }
    }
}
