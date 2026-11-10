namespace KhoiProjectManagement.Domain
{
    // One Kanban column per workflow status per project (created lazily with defaults on first board
    // load). Columns are a presentation/limit layer over ProjectTask.Status - moving a card changes the
    // task's real status, so the Tasks tab, reports and any future Scrum board all see the same data.
    public class BoardColumn : BaseEntity
    {
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;
        public string Status { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        // Max cards allowed in the column; null = unlimited. A move into a full column is rejected.
        public int? WipLimit { get; set; }
    }
}
