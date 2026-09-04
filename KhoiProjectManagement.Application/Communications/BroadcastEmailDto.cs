namespace KhoiProjectManagement.Application
{
    // Body is plain text (the frontend uses a plain textarea, not a rich-text editor - no such
    // dependency exists anywhere in this codebase) - BroadcastEmailService HTML-encodes it and
    // converts line breaks before it ever reaches an email, same reasoning as
    // EmailService.SendMentionEmailAsync's own encoding of free-form input.
    public class BroadcastEmailDto
    {
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<int> RoleIds { get; set; } = new();
    }

    public class BroadcastEmailResultDto
    {
        public int RecipientCount { get; set; }
    }
}
