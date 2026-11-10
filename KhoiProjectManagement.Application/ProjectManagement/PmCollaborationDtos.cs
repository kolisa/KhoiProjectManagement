namespace KhoiProjectManagement.Application
{
    public class WorkItemCommentDto
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        // Whether the calling user may edit/delete this comment (author, or someone who can delete items).
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class SaveCommentDto
    {
        public string Body { get; set; } = string.Empty;
    }

    public class ChecklistItemDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public int Position { get; set; }
    }

    public class SaveChecklistItemDto
    {
        public string Text { get; set; } = string.Empty;
        public bool IsDone { get; set; }
    }

    public static class WorkItemLinkTypes
    {
        public const string Blocks = "Blocks";
        public const string Relates = "Relates";
        public const string Duplicates = "Duplicates";
        public static readonly string[] All = { Blocks, Relates, Duplicates };
    }

    public class WorkItemLinkDto
    {
        public int Id { get; set; }
        // Wording from the viewed item's side: "Blocks", "Is blocked by", "Relates to", "Duplicates", "Is duplicated by".
        public string Relation { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsOutgoing { get; set; }
        public int OtherId { get; set; }
        public string OtherTitle { get; set; } = string.Empty;
        public string OtherType { get; set; } = string.Empty;
        public string OtherStatus { get; set; } = string.Empty;
    }

    public class CreateWorkItemLinkDto
    {
        public int TargetId { get; set; }
        public string Type { get; set; } = WorkItemLinkTypes.Relates;
    }

    public class WorkItemAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public bool IsImage { get; set; }
        public bool CanDelete { get; set; }
    }

    public class AttachmentUploadDto
    {
        public required Stream Content { get; init; }
        public required string FileName { get; init; }
        public required string ContentType { get; init; }
        public long Length { get; init; }
    }

    public class AttachmentDownload
    {
        public required Stream Content { get; init; }
        public required string FileName { get; init; }
        public required string ContentType { get; init; }
    }
}
