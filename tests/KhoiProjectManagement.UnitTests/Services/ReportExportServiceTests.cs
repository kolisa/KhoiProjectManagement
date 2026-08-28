using System.Text;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // ReportExportService.BuildPdf renders through QuestPDF's fluent document-building API directly
    // (QuestPDF.Fluent.Document.Create(...).GeneratePdf()). QuestPDF.Settings.License is only ever set
    // once, at startup, in the Api project's Program.cs - it's never set here in the test project - and
    // GeneratePdf() throws QuestPDF.Infrastructure.RequiredLicenseNotSpecifiedException without it.
    // Beyond that, asserting on raw PDF bytes wouldn't verify anything meaningful anyway. So PDF
    // rendering itself is left untested as a testability limitation; what *is* tested below is the
    // report-agnostic data-shaping (BuildRowsAsync's per-report-type header/row mapping) that runs
    // before either renderer, exercised through the CSV path, which is plain, assertable text.
    public class ReportExportServiceTests
    {
        private readonly IReportService _reportService = Substitute.For<IReportService>();
        private readonly IRepository<ReportExportHistory> _historyRepo = Substitute.For<IRepository<ReportExportHistory>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private ReportExportService CreateSut() => new(_reportService, _historyRepo, _unitOfWork);

        [Fact]
        public async Task ExportReportAsync_WhenReportTypeIsUnknown_Throws()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.ExportReportAsync("NotARealReport", ReportFormats.Csv, generatedByUserId: 1));
        }

        [Fact]
        public async Task ExportReportAsync_WhenFormatIsUnknown_Throws()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.ExportReportAsync(ReportTypes.ProjectSummary, "Xlsx", generatedByUserId: 1));
        }

        [Fact]
        public async Task ExportReportAsync_ProjectSummaryAsCsv_ShapesHeadersAndRowsFromTheReportData()
        {
            _reportService.GenerateProjectSummaryReportAsync().Returns(new ProjectSummaryReportDto
            {
                Projects = new List<ProjectSummaryItemDto>
                {
                    new() { Name = "Alpha", Status = "active", TasksCount = 4, CompletedTasks = 2, CompletionRate = 50.0 },
                }
            });

            var (content, contentType, fileName) = await CreateSut().ExportReportAsync(ReportTypes.ProjectSummary, ReportFormats.Csv, generatedByUserId: 7);

            var csv = Encoding.UTF8.GetString(content);
            Assert.Equal("text/csv", contentType);
            Assert.EndsWith(".csv", fileName);
            Assert.Contains("Project,Status,Tasks,Completed,Completion Rate", csv);
            Assert.Contains("Alpha,active,4,2,50%", csv);
        }

        [Fact]
        public async Task ExportReportAsync_TeamPerformanceAsCsv_ShapesHeadersAndRowsFromTheReportData()
        {
            _reportService.GenerateTeamPerformanceReportAsync().Returns(new TeamPerformanceReportDto
            {
                TeamMembers = new List<TeamMemberPerformanceDto>
                {
                    new() { Name = "Jane", Position = "Engineer", AssignedTasks = 4, CompletedTasks = 2, OverdueTasks = 1, CompletionRate = 50.0 },
                }
            });

            var (content, _, _) = await CreateSut().ExportReportAsync(ReportTypes.TeamPerformance, ReportFormats.Csv, generatedByUserId: 7);

            var csv = Encoding.UTF8.GetString(content);
            Assert.Contains("Name,Position,Assigned,Completed,Overdue,Completion Rate", csv);
            Assert.Contains("Jane,Engineer,4,2,1,50%", csv);
        }

        [Fact]
        public async Task ExportReportAsync_OverdueTasksAsCsv_ShapesHeadersAndRowsFromTheReportData()
        {
            var dueDate = new DateTime(2026, 1, 15);
            _reportService.GenerateOverdueTasksReportAsync().Returns(new OverdueTasksReportDto
            {
                Tasks = new List<OverdueTaskItemDto>
                {
                    new() { Title = "Fix bug", Project = "Alpha", AssignedTo = "Jane", DueDate = dueDate, DaysOverdue = 10, Priority = "high" },
                }
            });

            var (content, _, _) = await CreateSut().ExportReportAsync(ReportTypes.OverdueTasks, ReportFormats.Csv, generatedByUserId: 7);

            var csv = Encoding.UTF8.GetString(content);
            Assert.Contains("Task,Project,Assigned To,Due Date,Days Overdue,Priority", csv);
            Assert.Contains("Fix bug,Alpha,Jane,2026-01-15,10,high", csv);
        }

        [Fact]
        public async Task ExportReportAsync_EscapesCsvFieldsThatContainCommasOrQuotes()
        {
            _reportService.GenerateProjectSummaryReportAsync().Returns(new ProjectSummaryReportDto
            {
                Projects = new List<ProjectSummaryItemDto>
                {
                    new() { Name = "Alpha, Phase \"One\"", Status = "active", TasksCount = 1, CompletedTasks = 0, CompletionRate = 0 },
                }
            });

            var (content, _, _) = await CreateSut().ExportReportAsync(ReportTypes.ProjectSummary, ReportFormats.Csv, generatedByUserId: 7);

            var csv = Encoding.UTF8.GetString(content);
            Assert.Contains("\"Alpha, Phase \"\"One\"\"\",active,1,0,0%", csv);
        }

        [Fact]
        public async Task ExportReportAsync_PersistsExportHistoryAndSaves()
        {
            _reportService.GenerateProjectSummaryReportAsync().Returns(new ProjectSummaryReportDto());
            ReportExportHistory? added = null;
            _historyRepo.When(r => r.Add(Arg.Any<ReportExportHistory>())).Do(ci => added = ci.Arg<ReportExportHistory>());

            var (content, _, _) = await CreateSut().ExportReportAsync(ReportTypes.ProjectSummary, ReportFormats.Csv, generatedByUserId: 42);

            Assert.NotNull(added);
            Assert.Equal(ReportTypes.ProjectSummary, added!.ReportType);
            Assert.Equal(ReportFormats.Csv, added.Format);
            Assert.Equal(42, added.GeneratedByUserId);
            Assert.Equal(content.Length, added.FileSizeBytes);
            Assert.Equal(content, added.FileContent);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task DownloadExportAsync_WhenExportDoesNotExist_ReturnsNull()
        {
            _historyRepo.FindAsync(999).Returns((ReportExportHistory?)null);

            var result = await CreateSut().DownloadExportAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadExportAsync_WhenExportExists_ReturnsItsStoredContentAndContentType()
        {
            var export = new ReportExportHistory
            {
                Id = 1,
                ReportType = ReportTypes.OverdueTasks,
                Format = ReportFormats.Csv,
                GeneratedAt = new DateTime(2026, 2, 1),
                FileContent = Encoding.UTF8.GetBytes("Task,Project\r\n")
            };
            _historyRepo.FindAsync(1).Returns(export);

            var result = await CreateSut().DownloadExportAsync(1);

            Assert.NotNull(result);
            Assert.Equal(export.FileContent, result!.Value.Content);
            Assert.Equal("text/csv", result.Value.ContentType);
            Assert.Equal("OverdueTasks_2026-02-01.csv", result.Value.FileName);
        }

        [Fact]
        public async Task DownloadExportAsync_WhenFormatIsPdf_ReturnsPdfContentType()
        {
            var export = new ReportExportHistory
            {
                Id = 2,
                ReportType = ReportTypes.ProjectSummary,
                Format = ReportFormats.Pdf,
                GeneratedAt = new DateTime(2026, 2, 1),
                FileContent = new byte[] { 1, 2, 3 }
            };
            _historyRepo.FindAsync(2).Returns(export);

            var result = await CreateSut().DownloadExportAsync(2);

            Assert.NotNull(result);
            Assert.Equal("application/pdf", result!.Value.ContentType);
            Assert.Equal("ProjectSummary_2026-02-01.pdf", result.Value.FileName);
        }
    }
}
