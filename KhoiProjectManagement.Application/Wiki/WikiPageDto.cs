namespace KhoiProjectManagement.Application
{
    public class WikiPageSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public int? ParentPageId { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<string> Labels { get; set; } = new();
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
        public List<string> Labels { get; set; } = new();
    }

    public class MoveWikiPageDto
    {
        public int? NewParentPageId { get; set; }
    }

    // Full ordered list of sibling page IDs (same Space + ParentPageId) after a drag-and-drop reorder -
    // simpler than a single "move to position X" op since the frontend already has the full reshuffled
    // list in hand after a drop.
    public class ReorderWikiPagesDto
    {
        public List<int> OrderedPageIds { get; set; } = new();
    }

    public class SetWikiPageLabelsDto
    {
        public List<string> Labels { get; set; } = new();
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
        public int? AnchorBlockIndex { get; set; }
        public string? AnchorText { get; set; }
    }

    public class CreateWikiCommentDto
    {
        public string Body { get; set; } = string.Empty;
        public int? ParentCommentId { get; set; }
        public int? AnchorBlockIndex { get; set; }
        public string? AnchorText { get; set; }
    }

    public class WikiSearchResultDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public string SpaceName { get; set; } = string.Empty;

        // A short excerpt around the first match, not the full page - built client-side (C#) from
        // CurrentContentMarkdown rather than Postgres's ts_headline, which the Npgsql EF provider
        // doesn't expose as a translatable function.
        public string Snippet { get; set; } = string.Empty;
    }
}
