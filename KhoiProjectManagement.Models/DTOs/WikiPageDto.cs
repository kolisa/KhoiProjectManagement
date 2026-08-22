namespace KhoiProjectManagement.Models.DTOs
{
    public class WikiPageSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public int? ParentPageId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class WikiPageDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public int? ParentPageId { get; set; }
        public string ContentMarkdown { get; set; } = string.Empty;
        public int CurrentVersionNumber { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? LastEditedByName { get; set; }
        public DateTime? LastEditedAt { get; set; }
    }

    public class CreateWikiPageDto
    {
        public string Title { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public int? ParentPageId { get; set; }
        public string ContentMarkdown { get; set; } = string.Empty;
    }

    public class UpdateWikiPageDto
    {
        public string Title { get; set; } = string.Empty;
        public string ContentMarkdown { get; set; } = string.Empty;
        public string? EditSummary { get; set; }
    }

    public class WikiPageVersionSummaryDto
    {
        public int VersionNumber { get; set; }
        public string EditedByName { get; set; } = string.Empty;
        public DateTime EditedAt { get; set; }
        public string? EditSummary { get; set; }
    }

    public class WikiPageVersionDetailDto
    {
        public int VersionNumber { get; set; }
        public string ContentMarkdown { get; set; } = string.Empty;
        public string EditedByName { get; set; } = string.Empty;
        public DateTime EditedAt { get; set; }
        public string? EditSummary { get; set; }
    }

    public class WikiCommentDto
    {
        public int Id { get; set; }
        public int? ParentCommentId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public int AuthorId { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateWikiCommentDto
    {
        public string Body { get; set; } = string.Empty;
        public int? ParentCommentId { get; set; }
    }
}
