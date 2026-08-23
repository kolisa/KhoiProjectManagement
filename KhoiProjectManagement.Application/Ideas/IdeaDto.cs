namespace KhoiProjectManagement.Application
{
    public class IdeaDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int SubmittedBy { get; set; }
        public string SubmitterName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ConvertedProjectId { get; set; }
        public string? ConvertedProjectName { get; set; }
        public int CommentCount { get; set; }
    }

    public class IdeaCommentDto
    {
        public int Id { get; set; }
        public int AuthoredBy { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateIdeaDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class UpdateIdeaDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class IdeaStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class CreateIdeaCommentDto
    {
        public string Body { get; set; } = string.Empty;
    }

    public class IdeaAttachmentDto
    {
        public int Id { get; set; }
        public int IdeaId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int UploadedBy { get; set; }
        public string UploaderName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
        public int AnnotationCount { get; set; }
    }

    public class IdeaAttachmentAnnotationDto
    {
        public int Id { get; set; }
        public int AuthoredBy { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateIdeaAttachmentAnnotationDto
    {
        public string Body { get; set; } = string.Empty;
    }
}
