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
    public class PmReportMathTests
    {
        [Fact]
        public void Percentile_UsesNearestRank()
        {
            var data = new List<double> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            Assert.Equal(5, PmReportMath.Percentile(data, 50));
            Assert.Equal(9, PmReportMath.Percentile(data, 85));
            Assert.Equal(10, PmReportMath.Percentile(data, 100));
            Assert.Null(PmReportMath.Percentile(new List<double>(), 50));
        }

        [Fact]
        public void Stats_OfNothing_HasNoValues()
        {
            var s = PmReportMath.Stats(Array.Empty<double>());
            Assert.Equal(0, s.Samples);
            Assert.Null(s.Average);
            Assert.Null(s.Median);
        }

        [Fact]
        public void Stats_RoundsToOneDecimal()
        {
            var s = PmReportMath.Stats(new[] { 1.0, 2.0, 4.0 });
            Assert.Equal(3, s.Samples);
            Assert.Equal(2.3, s.Average);
            Assert.Equal(2.0, s.Median);
        }

        [Theory]
        [InlineData("2026-11-09", "2026-11-09")] // Monday
        [InlineData("2026-11-10", "2026-11-09")]
        [InlineData("2026-11-15", "2026-11-09")] // Sunday belongs to the week that started Monday
        [InlineData("2026-11-16", "2026-11-16")]
        public void WeekStart_IsTheMonday(string date, string expected) =>
            Assert.Equal(DateTime.Parse(expected), PmReportMath.WeekStart(DateTime.Parse(date)));

        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("a,b", "\"a,b\"")]
        [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [InlineData("line1\nline2", "\"line1\nline2\"")]
        [InlineData("=SUM(A1:A9)", "'=SUM(A1:A9)")]
        [InlineData("+1 555", "'+1 555")]
        [InlineData("-cmd", "'-cmd")]
        [InlineData("@home", "'@home")]
        [InlineData(null, "")]
        public void CsvCell_QuotesAndNeutralisesFormulas(string? input, string expected) =>
            Assert.Equal(expected, PmReportMath.CsvCell(input));
    }

    public class PmReportServiceTests
    {
        private static readonly PmCaller Dev = new(10, false);

        private readonly IRepository<ProjectTask> _tasks = Substitute.For<IRepository<ProjectTask>>();
        private readonly IRepository<TaskStatusHistory> _history = Substitute.For<IRepository<TaskStatusHistory>>();
        private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();

        private PmReportService CreateSut() => new(_tasks, _history, _access);
        private void SetTasks(params ProjectTask[] t) => _tasks.Query().Returns(_ => t.ToList().BuildMock());
        private void SetHistory(params TaskStatusHistory[] h) => _history.Query().Returns(_ => h.ToList().BuildMock());

        private static DateTime Ago(int days, int hours = 12) => DateTime.UtcNow.Date.AddDays(-days).AddHours(hours);

        [Fact]
        public async Task EveryReport_RequiresViewAccess()
        {
            _access.RequireAsync(Dev, 1, PmAction.View).Returns(Task.FromException(new KeyNotFoundException()));
            var sut = CreateSut();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetCumulativeFlowAsync(Dev, 1, 30));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetFlowMetricsAsync(Dev, 1, 30));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetWorkloadAsync(Dev, 1));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.ExportWorkItemsCsvAsync(Dev, 1));
        }

        [Fact]
        public async Task CumulativeFlow_ReplaysStatusHistoryDayByDay()
        {
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Task" }, new ProjectTask { Id = 2, ProjectId = 1, Type = "Task" });
            SetHistory(
                new TaskStatusHistory { TaskId = 1, ToStatus = "todo", ChangedAt = Ago(5) },
                new TaskStatusHistory { TaskId = 1, ToStatus = "in-progress", ChangedAt = Ago(3) },
                new TaskStatusHistory { TaskId = 1, ToStatus = "completed", ChangedAt = Ago(1) },
                new TaskStatusHistory { TaskId = 2, ToStatus = "todo", ChangedAt = Ago(2) });

            var cfd = await CreateSut().GetCumulativeFlowAsync(Dev, 1, 7);

            Assert.Equal(7, cfd.Points.Count);
            var byAgo = cfd.Points.ToDictionary(p => (DateTime.UtcNow.Date - p.Date).Days);
            Assert.Equal(1, byAgo[4].Counts["todo"]);                          // item 1 created, item 2 not yet
            Assert.Equal(1, byAgo[3].Counts["in-progress"]);
            Assert.Equal(1, byAgo[2].Counts["todo"]);                          // item 2 arrives
            Assert.Equal(1, byAgo[2].Counts["in-progress"]);
            Assert.Equal(1, byAgo[0].Counts["completed"]);
            Assert.Equal(1, byAgo[0].Counts["todo"]);
            Assert.Equal(0, byAgo[6].Counts.Values.Sum());                     // before anything existed
        }

        [Fact]
        public async Task CumulativeFlow_ItemWithNoTransitionsInTheWindow_KeepsItsCurrentStatus_FromItsCreation()
        {
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Status = "in-progress", CreatedAt = Ago(40) },   // long-lived, untouched
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Status = "completed", CreatedAt = Ago(3) });    // created in window: has its own creation row
            SetHistory(new TaskStatusHistory { TaskId = 2, FromStatus = null, ToStatus = "todo", ChangedAt = Ago(3) },
                       new TaskStatusHistory { TaskId = 2, FromStatus = "todo", ToStatus = "completed", ChangedAt = Ago(1) });

            var cfd = await CreateSut().GetCumulativeFlowAsync(Dev, 1, 7);

            var byAgo = cfd.Points.ToDictionary(p => (DateTime.UtcNow.Date - p.Date).Days);
            Assert.All(cfd.Points, p => Assert.Equal(1, p.Counts["in-progress"]));
            Assert.Equal(0, byAgo[4].Counts["todo"] + byAgo[4].Counts["completed"]);   // item 2 not created yet
            Assert.Equal(1, byAgo[3].Counts["todo"]);
            Assert.Equal(1, byAgo[0].Counts["completed"]);
        }

        [Fact]
        public async Task CumulativeFlow_UsesTheFromStatusOfTheFirstInWindowTransition_ForTheStartOfTheWindow()
        {
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Status = "completed", CreatedAt = Ago(60) });
            SetHistory(new TaskStatusHistory { TaskId = 1, FromStatus = "blocked", ToStatus = "completed", ChangedAt = Ago(2) });

            var cfd = await CreateSut().GetCumulativeFlowAsync(Dev, 1, 7);

            var byAgo = cfd.Points.ToDictionary(p => (DateTime.UtcNow.Date - p.Date).Days);
            Assert.Equal(1, byAgo[6].Counts["blocked"]);
            Assert.Equal(1, byAgo[2].Counts["completed"]);
        }

        [Fact]
        public async Task CumulativeFlow_ExcludesEpicsAndFeatures_AndClampsTheRange()
        {
            SetTasks(new ProjectTask { Id = 1, ProjectId = 1, Type = "Epic" }, new ProjectTask { Id = 2, ProjectId = 1, Type = "Feature" });
            SetHistory(new TaskStatusHistory { TaskId = 1, ToStatus = "todo", ChangedAt = Ago(2) });

            var cfd = await CreateSut().GetCumulativeFlowAsync(Dev, 1, 9999);

            Assert.Equal(180, cfd.Points.Count);
            Assert.All(cfd.Points, p => Assert.Equal(0, p.Counts.Values.Sum()));
        }

        [Fact]
        public async Task FlowMetrics_ComputesLeadAndCycleTime_IgnoringNeverStartedItemsForCycle()
        {
            var now = DateTime.UtcNow;
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Story", Status = "completed", StoryPoints = 5, CreatedAt = now.AddDays(-10), StartedAt = now.AddDays(-6), CompletedAt = now.AddDays(-2) },
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Status = "completed", StoryPoints = 3, CreatedAt = now.AddDays(-4), StartedAt = null, CompletedAt = now.AddDays(-1) },
                new ProjectTask { Id = 3, ProjectId = 1, Type = "Task", Status = "todo", CreatedAt = now.AddDays(-4) },
                new ProjectTask { Id = 4, ProjectId = 1, Type = "Epic", Status = "completed", CreatedAt = now.AddDays(-9), CompletedAt = now.AddDays(-1) },
                new ProjectTask { Id = 5, ProjectId = 1, Type = "Task", Status = "completed", CreatedAt = now.AddDays(-200), CompletedAt = now.AddDays(-150) });

            var m = await CreateSut().GetFlowMetricsAsync(Dev, 1, 30);

            Assert.Equal(2, m.CompletedCount);          // epic and out-of-window item excluded
            Assert.Equal(8, m.CompletedPoints);
            Assert.Equal(2, m.LeadTime.Samples);
            Assert.Equal(1, m.CycleTime.Samples);       // only item 1 was ever started
            Assert.Equal(4.0, m.CycleTime.Average);
            Assert.Equal(3.0, m.LeadTime.Median);       // lead times 3 and 8 days -> nearest-rank median is the lower
            Assert.Equal(8, m.Weeks.Sum(w => w.Points));
            Assert.Equal(2, m.Weeks.Sum(w => w.Items));
        }

        [Fact]
        public async Task Workload_GroupsOpenWork_PerAssignee_AndLabelsUnassigned()
        {
            var now = DateTime.UtcNow;
            var dana = new User { Id = 1, Name = "Dana" };
            SetTasks(
                new ProjectTask { Id = 1, ProjectId = 1, Type = "Task", Status = "in-progress", StoryPoints = 5, AssignedToId = 1, AssignedTo = dana, DueDate = now.AddDays(-1) },
                new ProjectTask { Id = 2, ProjectId = 1, Type = "Task", Status = "blocked", StoryPoints = 3, AssignedToId = 1, AssignedTo = dana, DueDate = now.AddDays(5) },
                new ProjectTask { Id = 3, ProjectId = 1, Type = "Task", Status = "completed", AssignedToId = 1, AssignedTo = dana, CompletedAt = now.AddDays(-3), DueDate = now },
                new ProjectTask { Id = 4, ProjectId = 1, Type = "Task", Status = "completed", AssignedToId = 1, AssignedTo = dana, CompletedAt = now.AddDays(-90), DueDate = now },
                new ProjectTask { Id = 5, ProjectId = 1, Type = "Bug", Status = "todo", StoryPoints = 2, DueDate = now.AddDays(1) },
                new ProjectTask { Id = 6, ProjectId = 1, Type = "Epic", Status = "todo", StoryPoints = 40, DueDate = now.AddDays(1) });

            var rows = await CreateSut().GetWorkloadAsync(Dev, 1);

            Assert.Equal(2, rows.Count);
            var d = rows[0];
            Assert.Equal("Dana", d.Name);
            Assert.Equal(2, d.OpenItems);
            Assert.Equal(8, d.OpenPoints);
            Assert.Equal(1, d.InProgress);
            Assert.Equal(1, d.Blocked);
            Assert.Equal(1, d.Overdue);
            Assert.Equal(1, d.DoneLast30Days);          // the 90-day-old completion is outside the window
            Assert.Equal("Unassigned", rows[1].Name);
            Assert.Equal(2, rows[1].OpenPoints);        // epic's 40 points are not counted
        }

        [Fact]
        public async Task Export_WritesBomHeaderAndNeutralisesFormulaTitles()
        {
            SetTasks(new ProjectTask
            {
                Id = 3, ProjectId = 1, Type = "Bug", Title = "=HYPERLINK(\"x\"), broken", Status = "todo", Priority = "high", StoryPoints = 2,
                CreatedAt = new DateTime(2026, 11, 1), DueDate = new DateTime(2026, 11, 20),
            });

            var export = await CreateSut().ExportWorkItemsCsvAsync(Dev, 1);

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, export.Content.Take(3).ToArray());
            var text = Encoding.UTF8.GetString(export.Content).TrimStart('﻿');
            var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.StartsWith("Id,Type,Title,Status", lines[0]);
            Assert.Equal("3,Bug,\"'=HYPERLINK(\"\"x\"\"), broken\",todo,high,2,,,,2026-11-01,,,2026-11-20", lines[1]);
            Assert.Matches(@"^project-1-work-items-\d{8}\.csv$", export.FileName);
        }
    }
}
