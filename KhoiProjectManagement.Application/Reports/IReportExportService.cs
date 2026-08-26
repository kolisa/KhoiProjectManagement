namespace KhoiProjectManagement.Application
{
    public interface IReportExportService
    {
        // Generates the report, persists it as a ReportExportHistory row, and returns the bytes for
        // immediate download. reportType must be one of ReportTypes.All, format one of ReportFormats.All.
        Task<(byte[] Content, string ContentType, string FileName)> ExportReportAsync(string reportType, string format, int generatedByUserId);

        Task<(byte[] Content, string ContentType, string FileName)?> DownloadExportAsync(int exportHistoryId);
    }
}
