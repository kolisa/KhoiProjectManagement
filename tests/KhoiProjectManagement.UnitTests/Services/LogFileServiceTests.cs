using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // Writes real temp files rather than mocking - LogFileService's whole job is file-system tailing/
    // parsing, so an InMemory-style substitute wouldn't exercise the actual logic being tested (same
    // reasoning as SpacePermissionResolverTests using a real InMemory DbContext instead of mocks).
    public class LogFileServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public LogFileServiceTests()
        {
            _tempDir = Directory.CreateTempSubdirectory("khoi-logtest-").FullName;
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        private LogFileService CreateSut()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["LogViewer:Directory"] = _tempDir })
                .Build();
            return new LogFileService(config);
        }

        private void WriteLogFile(string dateStamp, string content) =>
            File.WriteAllText(Path.Combine(_tempDir, $"log-{dateStamp}.txt"), content);

        [Fact]
        public async Task GetAvailableDatesAsync_ReturnsOnlyLogFilesNewestFirst()
        {
            WriteLogFile("20260101", "irrelevant");
            WriteLogFile("20260103", "irrelevant");
            WriteLogFile("20260102", "irrelevant");
            File.WriteAllText(Path.Combine(_tempDir, "not-a-log-file.txt"), "ignore me");

            var result = await CreateSut().GetAvailableDatesAsync();

            Assert.Equal(3, result.Count);
            Assert.Equal(new DateOnly(2026, 1, 3), result[0]);
            Assert.Equal(new DateOnly(2026, 1, 2), result[1]);
            Assert.Equal(new DateOnly(2026, 1, 1), result[2]);
        }

        [Fact]
        public async Task GetAvailableDatesAsync_WhenDirectoryDoesNotExist_ReturnsEmptyList()
        {
            Directory.Delete(_tempDir);

            var result = await CreateSut().GetAvailableDatesAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRecentEntriesAsync_WhenNoFileExistsForThatDate_ReturnsEmptyListNotAnError()
        {
            var result = await CreateSut().GetRecentEntriesAsync(new DateOnly(2026, 1, 1), levelFilter: null, take: 10);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRecentEntriesAsync_ParsesEntriesNewestFirstAndGroupsExceptionLinesIntoTheParentEntry()
        {
            var content =
                "2026-01-01 09:00:00.000 +00:00 [INF] Application started\n" +
                "2026-01-01 09:00:01.000 +00:00 [WRN] Something looked odd\n" +
                "2026-01-01 09:00:02.000 +00:00 [ERR] Request failed\n" +
                "System.Exception: boom\n" +
                "   at Some.Method()\n";
            WriteLogFile("20260101", content);

            var result = await CreateSut().GetRecentEntriesAsync(new DateOnly(2026, 1, 1), levelFilter: null, take: 10);

            Assert.Equal(3, result.Count);
            Assert.Equal("ERR", result[0].Level); // newest first
            Assert.Contains("Request failed", result[0].Message);
            Assert.Contains("System.Exception: boom", result[0].Message);
            Assert.Contains("at Some.Method()", result[0].Message);
            Assert.Equal("WRN", result[1].Level);
            Assert.Equal("INF", result[2].Level);
        }

        [Fact]
        public async Task GetRecentEntriesAsync_WithErrorLevelFilter_MatchesBothErrAndFtlButNotWrnOrInf()
        {
            var content =
                "2026-01-01 09:00:00.000 +00:00 [INF] Started\n" +
                "2026-01-01 09:00:01.000 +00:00 [WRN] Warned\n" +
                "2026-01-01 09:00:02.000 +00:00 [ERR] Errored\n" +
                "2026-01-01 09:00:03.000 +00:00 [FTL] Fatal crash\n";
            WriteLogFile("20260101", content);

            var result = await CreateSut().GetRecentEntriesAsync(new DateOnly(2026, 1, 1), levelFilter: "Error", take: 10);

            Assert.Equal(2, result.Count);
            Assert.All(result, e => Assert.True(e.Level is "ERR" or "FTL"));
        }

        [Fact]
        public async Task GetRecentEntriesAsync_WithWarningLevelFilter_MatchesOnlyWrn()
        {
            var content =
                "2026-01-01 09:00:00.000 +00:00 [INF] Started\n" +
                "2026-01-01 09:00:01.000 +00:00 [WRN] Warned\n" +
                "2026-01-01 09:00:02.000 +00:00 [ERR] Errored\n";
            WriteLogFile("20260101", content);

            var result = await CreateSut().GetRecentEntriesAsync(new DateOnly(2026, 1, 1), levelFilter: "Warning", take: 10);

            Assert.Single(result);
            Assert.Equal("WRN", result[0].Level);
        }

        [Fact]
        public async Task GetRecentEntriesAsync_RespectsTake()
        {
            var content = string.Concat(Enumerable.Range(1, 5).Select(i =>
                $"2026-01-01 09:00:0{i}.000 +00:00 [INF] Entry {i}\n"));
            WriteLogFile("20260101", content);

            var result = await CreateSut().GetRecentEntriesAsync(new DateOnly(2026, 1, 1), levelFilter: null, take: 2);

            Assert.Equal(2, result.Count);
            Assert.Contains("Entry 5", result[0].Message);
            Assert.Contains("Entry 4", result[1].Message);
        }
    }
}
