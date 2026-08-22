namespace KhoiProjectManagement.Models.DTOs
{
    public class LibraryFileDto
    {
        public int Id { get; set; }
        public int SpaceId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int CurrentVersionNumber { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUploadedAt { get; set; }
    }

    public class LibraryFileVersionDto
    {
        public int VersionNumber { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Comment { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }
}
