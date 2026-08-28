using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class TimesheetServiceTests
    {
        private readonly IRepository<Timesheet> _timesheetRepo = Substitute.For<IRepository<Timesheet>>();
        private readonly IRepository<TimesheetEntry> _entryRepo = Substitute.For<IRepository<TimesheetEntry>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private TimesheetService CreateSut() => new(_timesheetRepo, _entryRepo, _unitOfWork);

        private static ClaimsPrincipal CallerWithId(int userId, params string[] permissions)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        // --- GetTimesheetsAsync ---

        [Fact]
        public async Task GetTimesheetsAsync_WhenUserIdIsOmitted_ReturnsCallersOwnTimesheets()
        {
            var mine = new Timesheet { Id = 1, UserId = 1, Status = "Draft", User = new User { Id = 1, Name = "Me" } };
            var someoneElses = new Timesheet { Id = 2, UserId = 2, Status = "Draft", User = new User { Id = 2, Name = "Other" } };
            _timesheetRepo.Query().Returns(new List<Timesheet> { mine, someoneElses }.BuildMock());

            var result = await CreateSut().GetTimesheetsAsync(null, null, CallerWithId(1));

            Assert.Single(result);
            Assert.Equal(1, result[0].Id);
        }

        [Fact]
        public async Task GetTimesheetsAsync_WhenRequestingAnotherUsersTimesheetsWithoutPermission_Throws()
        {
            _timesheetRepo.Query().Returns(new List<Timesheet>().BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().GetTimesheetsAsync(2, null, CallerWithId(1)));
        }

        [Fact]
        public async Task GetTimesheetsAsync_WhenRequestingAnotherUsersTimesheetsWithViewAllPermission_ReturnsThem()
        {
            var target = new Timesheet { Id = 5, UserId = 2, Status = "Draft", User = new User { Id = 2, Name = "Other" } };
            _timesheetRepo.Query().Returns(new List<Timesheet> { target }.BuildMock());

            var result = await CreateSut().GetTimesheetsAsync(2, null, CallerWithId(1, "timesheets.view_all"));

            Assert.Single(result);
            Assert.Equal(5, result[0].Id);
        }

        [Fact]
        public async Task GetTimesheetsAsync_WhenStatusProvided_FiltersByStatus()
        {
            var draft = new Timesheet { Id = 1, UserId = 1, Status = "Draft", User = new User { Id = 1, Name = "Me" } };
            var submitted = new Timesheet { Id = 2, UserId = 1, Status = "Submitted", User = new User { Id = 1, Name = "Me" } };
            _timesheetRepo.Query().Returns(new List<Timesheet> { draft, submitted }.BuildMock());

            var result = await CreateSut().GetTimesheetsAsync(null, "Submitted", CallerWithId(1));

            Assert.Single(result);
            Assert.Equal(2, result[0].Id);
        }

        [Fact]
        public async Task GetTimesheetsAsync_ComputesTotalHoursFromEntries()
        {
            var timesheet = new Timesheet
            {
                Id = 1,
                UserId = 1,
                Status = "Draft",
                User = new User { Id = 1, Name = "Me" },
                Entries = new List<TimesheetEntry>
                {
                    new() { Id = 1, Hours = 3.5m, EntryDate = new DateTime(2026, 1, 1) },
                    new() { Id = 2, Hours = 4m, EntryDate = new DateTime(2026, 1, 2) },
                }
            };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            var result = await CreateSut().GetTimesheetsAsync(null, null, CallerWithId(1));

            Assert.Equal(7.5m, result[0].TotalHours);
        }

        // --- GetTimesheetByIdAsync ---

        [Fact]
        public async Task GetTimesheetByIdAsync_WhenNotFound_ReturnsNull()
        {
            _timesheetRepo.Query().Returns(new List<Timesheet>().BuildMock());

            var result = await CreateSut().GetTimesheetByIdAsync(999, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetTimesheetByIdAsync_WhenOwnedByCaller_ReturnsIt()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 1, Status = "Draft", User = new User { Id = 1, Name = "Me" } };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            var result = await CreateSut().GetTimesheetByIdAsync(1, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal(1, result!.Id);
        }

        [Fact]
        public async Task GetTimesheetByIdAsync_WhenNotOwnedAndCallerLacksViewAllOrApprove_Throws()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Draft", User = new User { Id = 2, Name = "Other" } };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().GetTimesheetByIdAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task GetTimesheetByIdAsync_WhenNotOwnedButCallerHasApprovePermission_ReturnsIt()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Submitted", User = new User { Id = 2, Name = "Other" } };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            var result = await CreateSut().GetTimesheetByIdAsync(1, CallerWithId(1, "timesheets.approve"));

            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetTimesheetByIdAsync_WhenNotOwnedButCallerHasViewAllPermission_ReturnsIt()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Draft", User = new User { Id = 2, Name = "Other" } };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            var result = await CreateSut().GetTimesheetByIdAsync(1, CallerWithId(1, "timesheets.view_all"));

            Assert.NotNull(result);
        }

        // --- CreateTimesheetAsync ---

        [Fact]
        public async Task CreateTimesheetAsync_AddsDraftTimesheetOwnedByCallerWithMappedEntries()
        {
            Timesheet? added = null;
            _timesheetRepo.When(r => r.Add(Arg.Any<Timesheet>())).Do(ci =>
            {
                added = ci.Arg<Timesheet>();
                added.Id = 7;
            });
            // LoadAsync re-queries after save - return the same instance the service just added.
            _timesheetRepo.Query().Returns(_ => new List<Timesheet> { added! }.BuildMock());

            var dto = new CreateTimesheetDto
            {
                PeriodStart = new DateTime(2026, 1, 1),
                PeriodEnd = new DateTime(2026, 1, 7),
                Entries = new List<CreateTimesheetEntryDto>
                {
                    new() { EntryDate = new DateTime(2026, 1, 1), ProjectId = 3, Description = "Work", Hours = 8m }
                }
            };

            var result = await CreateSut().CreateTimesheetAsync(dto, CallerWithId(1));

            Assert.Equal("Draft", result.Status);
            Assert.Equal(1, result.UserId);
            Assert.Single(result.Entries);
            Assert.Equal(8m, result.Entries[0].Hours);
            _timesheetRepo.Received(1).Add(Arg.Is<Timesheet>(t =>
                t.UserId == 1 && t.Status == "Draft" && t.Entries.Count == 1));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // --- UpdateTimesheetAsync ---

        [Fact]
        public async Task UpdateTimesheetAsync_WhenTimesheetDoesNotExist_ReturnsFalse()
        {
            _timesheetRepo.Query().Returns(new List<Timesheet>().BuildMock());

            var result = await CreateSut().UpdateTimesheetAsync(999, new UpdateTimesheetDto(), CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateTimesheetAsync_WhenNotOwnedByCaller_Throws()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Draft", Entries = new List<TimesheetEntry>() };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().UpdateTimesheetAsync(1, new UpdateTimesheetDto(), CallerWithId(1)));
        }

        [Theory]
        [InlineData("Submitted")]
        [InlineData("Approved")]
        public async Task UpdateTimesheetAsync_WhenStatusIsNotDraftOrRejected_Throws(string status)
        {
            var timesheet = new Timesheet { Id = 1, UserId = 1, Status = status, Entries = new List<TimesheetEntry>() };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().UpdateTimesheetAsync(1, new UpdateTimesheetDto(), CallerWithId(1)));
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("Rejected")]
        public async Task UpdateTimesheetAsync_WhenStatusIsDraftOrRejected_ReplacesEntries(string status)
        {
            var oldEntry = new TimesheetEntry { Id = 1, Hours = 2m, EntryDate = new DateTime(2026, 1, 1) };
            var timesheet = new Timesheet
            {
                Id = 1,
                UserId = 1,
                Status = status,
                Entries = new List<TimesheetEntry> { oldEntry }
            };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            var dto = new UpdateTimesheetDto
            {
                Entries = new List<CreateTimesheetEntryDto>
                {
                    new() { EntryDate = new DateTime(2026, 1, 2), Hours = 5m, Description = "New" }
                }
            };

            var result = await CreateSut().UpdateTimesheetAsync(1, dto, CallerWithId(1));

            Assert.True(result);
            _entryRepo.Received(1).RemoveRange(Arg.Is<IEnumerable<TimesheetEntry>>(rows => rows.Single() == oldEntry));
            Assert.Single(timesheet.Entries);
            Assert.Equal(5m, timesheet.Entries.Single().Hours);
            Assert.Equal(1, timesheet.Entries.Single().TimesheetId);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // --- SubmitTimesheetAsync ---

        [Fact]
        public async Task SubmitTimesheetAsync_WhenTimesheetDoesNotExist_ReturnsFalse()
        {
            _timesheetRepo.Query().Returns(new List<Timesheet>().BuildMock());

            var result = await CreateSut().SubmitTimesheetAsync(999, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task SubmitTimesheetAsync_WhenNotOwnedByCaller_Throws()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Draft" };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => CreateSut().SubmitTimesheetAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task SubmitTimesheetAsync_WhenAlreadyApproved_Throws()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 1, Status = "Approved" };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().SubmitTimesheetAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task SubmitTimesheetAsync_WhenRejected_ResubmitsAndClearsRejectionReason()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 1, Status = "Rejected", RejectionReason = "Missing details" };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            var result = await CreateSut().SubmitTimesheetAsync(1, CallerWithId(1));

            Assert.True(result);
            Assert.Equal("Submitted", timesheet.Status);
            Assert.Null(timesheet.RejectionReason);
            Assert.NotNull(timesheet.SubmittedAt);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // --- ApproveTimesheetAsync ---

        [Fact]
        public async Task ApproveTimesheetAsync_WhenTimesheetDoesNotExist_ReturnsFalse()
        {
            _timesheetRepo.Query().Returns(new List<Timesheet>().BuildMock());

            var result = await CreateSut().ApproveTimesheetAsync(999, CallerWithId(1, "timesheets.approve"));

            Assert.False(result);
        }

        [Fact]
        public async Task ApproveTimesheetAsync_WhenNotSubmitted_Throws()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Draft" };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().ApproveTimesheetAsync(1, CallerWithId(1, "timesheets.approve")));
        }

        [Fact]
        public async Task ApproveTimesheetAsync_WhenSubmitted_SetsApprovedStatusAndApprover()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Submitted" };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            var result = await CreateSut().ApproveTimesheetAsync(1, CallerWithId(9, "timesheets.approve"));

            Assert.True(result);
            Assert.Equal("Approved", timesheet.Status);
            Assert.Equal(9, timesheet.ApprovedBy);
            Assert.NotNull(timesheet.ApprovedAt);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // --- RejectTimesheetAsync ---

        [Fact]
        public async Task RejectTimesheetAsync_WhenTimesheetDoesNotExist_ReturnsFalse()
        {
            _timesheetRepo.Query().Returns(new List<Timesheet>().BuildMock());

            var result = await CreateSut().RejectTimesheetAsync(999, "Bad data", CallerWithId(1, "timesheets.approve"));

            Assert.False(result);
        }

        [Fact]
        public async Task RejectTimesheetAsync_WhenNotSubmitted_Throws()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Draft" };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateSut().RejectTimesheetAsync(1, "Bad data", CallerWithId(1, "timesheets.approve")));
        }

        [Fact]
        public async Task RejectTimesheetAsync_WhenSubmitted_SetsRejectedStatusReasonAndApprover()
        {
            var timesheet = new Timesheet { Id = 1, UserId = 2, Status = "Submitted" };
            _timesheetRepo.Query().Returns(new List<Timesheet> { timesheet }.BuildMock());

            var result = await CreateSut().RejectTimesheetAsync(1, "Missing hours breakdown", CallerWithId(9, "timesheets.approve"));

            Assert.True(result);
            Assert.Equal("Rejected", timesheet.Status);
            Assert.Equal("Missing hours breakdown", timesheet.RejectionReason);
            Assert.Equal(9, timesheet.ApprovedBy);
            Assert.NotNull(timesheet.ApprovedAt);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }
    }
}
