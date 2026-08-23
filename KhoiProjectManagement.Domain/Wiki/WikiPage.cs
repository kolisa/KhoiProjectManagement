namespace KhoiProjectManagement.Domain
{
    // A wiki page lives inside a Space and is authorized exactly like VaultEntry - proving the
    // Space/SpacePermission model genuinely generalizes, not just fits the vault. ParentPageId nests
    // pages within one Space for navigation only; it is not a permission boundary - Space.ParentSpaceId
    // remains the only thing that scopes access.
    public class WikiPage : BaseEntity, ISpaceScoped
    {
        public string Title { get; set; } = string.Empty;

        // Denormalized copy of the latest WikiPageVersion's content (the version table remains the
        // source of truth for history) - kept in sync by WikiService whenever a new version is
        // written. Lets full-text search run a single-table query instead of joining/aggregating
        // WikiPageVersions on every search. The tsvector match itself is computed at query time
        // (EF.Functions.ToTsVector, in WikiService.SearchPagesAsync) rather than as a stored generated
        // column - simpler, no Postgres-specific type on this shared entity, and plenty fast at the
        // scale a company wiki actually reaches. Revisit with a GIN-indexed generated column only if
        // page count grows into the tens of thousands.
        public string? CurrentContentMarkdown { get; set; }

        public int SpaceId { get; set; }
        public virtual Space Space { get; set; } = null!;

        public int? ParentPageId { get; set; }
        public virtual WikiPage? ParentPage { get; set; }
        public virtual ICollection<WikiPage> ChildPages { get; set; } = new List<WikiPage>();

        // Position among siblings sharing the same (SpaceId, ParentPageId) - drives drag-and-drop
        // reorder in the page list. Renumbered sequentially (0, 1, 2...) on every move rather than
        // using fractional indexing - simpler, and O(n) per move is irrelevant at the sibling-count a
        // wiki page list actually reaches.
        public int SortOrder { get; set; }

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;

        public int? UpdatedBy { get; set; }
        public virtual User? Updater { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<WikiPageVersion> Versions { get; set; } = new List<WikiPageVersion>();
        public virtual ICollection<WikiPageComment> Comments { get; set; } = new List<WikiPageComment>();
        public virtual ICollection<WikiPageTag> WikiPageTags { get; set; } = new List<WikiPageTag>();
    }
}
