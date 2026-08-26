namespace KhoiProjectManagement.Application
{
    public class GlobalSearchResultDto
    {
        public List<GlobalSearchItemDto> Projects { get; set; } = new();
        public List<GlobalSearchItemDto> Tasks { get; set; } = new();
        public List<GlobalSearchItemDto> People { get; set; } = new();
    }

    public class GlobalSearchItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
    }
}
