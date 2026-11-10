namespace KhoiProjectManagement.Application
{
    public class BoardColumnDto
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        public int? WipLimit { get; set; }
        public int Count { get; set; }
        public bool OverWip => WipLimit.HasValue && Count > WipLimit.Value;
        public List<WorkItemDto> Items { get; set; } = new();
    }

    public class BoardDto
    {
        public int ProjectId { get; set; }
        public List<BoardColumnDto> Columns { get; set; } = new();
    }

    public class SaveBoardColumnDto
    {
        public string Status { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int? WipLimit { get; set; }
    }

    public class MoveWorkItemDto
    {
        public string Status { get; set; } = string.Empty;
        // Place after this card in the target column; null = top of the column.
        public int? AfterItemId { get; set; }
    }
}
