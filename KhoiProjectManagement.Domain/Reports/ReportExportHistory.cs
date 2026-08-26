namespace KhoiProjectManagement.Domain
{
    // First Domain entity Reports has ever needed - until now Reports was pure read-aggregation with
    // no persistence (see CLAUDE.md). Generated files are stored inline (FileContent) rather than in
    // separate blob storage - fine at this scale, and it keeps "download an old export" a single-table
    // read instead of a second storage dependency.
    public class ReportExportHistory : BaseEntity
    {
        public string ReportType { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;

        public int GeneratedByUserId { get; set; }
        public virtual User GeneratedByUser { get; set; } = null!;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public long FileSizeBytes { get; set; }
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
    }
}
