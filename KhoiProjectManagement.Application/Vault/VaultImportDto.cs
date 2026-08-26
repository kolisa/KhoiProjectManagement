namespace KhoiProjectManagement.Application
{
    public class VaultImportResultDto
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
