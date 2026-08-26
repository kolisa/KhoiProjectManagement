using System.Text;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KhoiProjectManagement.Application
{
    public class ReportExportService : IReportExportService
    {
        private readonly IReportService _reportService;
        private readonly IRepository<ReportExportHistory> _historyRepo;
        private readonly IUnitOfWork _unitOfWork;

        public ReportExportService(IReportService reportService, IRepository<ReportExportHistory> historyRepo, IUnitOfWork unitOfWork)
        {
            _reportService = reportService;
            _historyRepo = historyRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<(byte[] Content, string ContentType, string FileName)> ExportReportAsync(string reportType, string format, int generatedByUserId)
        {
            if (!ReportTypes.IsValid(reportType))
                throw new InvalidOperationException($"Unknown report type '{reportType}'.");
            if (!ReportFormats.IsValid(format))
                throw new InvalidOperationException($"Unknown format '{format}'. Must be one of: {string.Join(", ", ReportFormats.All)}.");

            var (title, headers, rows) = await BuildRowsAsync(reportType);

            var content = format == ReportFormats.Pdf
                ? BuildPdf(title, headers, rows)
                : BuildCsv(headers, rows);

            var contentType = format == ReportFormats.Pdf ? "application/pdf" : "text/csv";
            var extension = format == ReportFormats.Pdf ? "pdf" : "csv";
            var fileName = $"{title.Replace(' ', '_')}_{DateTime.UtcNow:yyyy-MM-dd}.{extension}";

            _historyRepo.Add(new ReportExportHistory
            {
                ReportType = reportType,
                Format = format,
                GeneratedByUserId = generatedByUserId,
                GeneratedAt = DateTime.UtcNow,
                FileSizeBytes = content.Length,
                FileContent = content
            });
            await _unitOfWork.SaveChangesAsync();

            return (content, contentType, fileName);
        }

        public async Task<(byte[] Content, string ContentType, string FileName)?> DownloadExportAsync(int exportHistoryId)
        {
            var export = await _historyRepo.FindAsync(exportHistoryId);
            if (export == null)
                return null;

            var contentType = export.Format == ReportFormats.Pdf ? "application/pdf" : "text/csv";
            var extension = export.Format == ReportFormats.Pdf ? "pdf" : "csv";
            var fileName = $"{export.ReportType}_{export.GeneratedAt:yyyy-MM-dd}.{extension}";

            return (export.FileContent, contentType, fileName);
        }

        private async Task<(string Title, string[] Headers, List<string[]> Rows)> BuildRowsAsync(string reportType)
        {
            if (reportType == ReportTypes.ProjectSummary)
            {
                var report = await _reportService.GenerateProjectSummaryReportAsync();
                var headers = new[] { "Project", "Status", "Tasks", "Completed", "Completion Rate" };
                var rows = report.Projects
                    .Select(p => new[] { p.Name, p.Status, p.TasksCount.ToString(), p.CompletedTasks.ToString(), $"{p.CompletionRate:0.#}%" })
                    .ToList();
                return (report.Title, headers, rows);
            }

            if (reportType == ReportTypes.TeamPerformance)
            {
                var report = await _reportService.GenerateTeamPerformanceReportAsync();
                var headers = new[] { "Name", "Position", "Assigned", "Completed", "Overdue", "Completion Rate" };
                var rows = report.TeamMembers
                    .Select(m => new[] { m.Name, m.Position, m.AssignedTasks.ToString(), m.CompletedTasks.ToString(), m.OverdueTasks.ToString(), $"{m.CompletionRate:0.#}%" })
                    .ToList();
                return (report.Title, headers, rows);
            }

            var overdue = await _reportService.GenerateOverdueTasksReportAsync();
            var overdueHeaders = new[] { "Task", "Project", "Assigned To", "Due Date", "Days Overdue", "Priority" };
            var overdueRows = overdue.Tasks
                .Select(t => new[] { t.Title, t.Project, t.AssignedTo, t.DueDate.ToString("yyyy-MM-dd"), t.DaysOverdue.ToString(), t.Priority })
                .ToList();
            return (overdue.Title, overdueHeaders, overdueRows);
        }

        private static byte[] BuildCsv(string[] headers, List<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
                return $"\"{field.Replace("\"", "\"\"")}\"";
            return field;
        }

        private static byte[] BuildPdf(string title, string[] headers, List<string[]> rows)
        {
            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(title).FontSize(18).Bold();
                        col.Item().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    page.Content().PaddingTop(15).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in headers)
                                columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            foreach (var h in headers)
                                header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(h).Bold();
                        });

                        foreach (var row in rows)
                        {
                            foreach (var cell in row)
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(cell);
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
