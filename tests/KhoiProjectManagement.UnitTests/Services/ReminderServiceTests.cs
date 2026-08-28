using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class ReminderServiceTests
    {
        private readonly IRepository<Reminder> _reminderRepo = Substitute.For<IRepository<Reminder>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<Notification> _notificationRepo = Substitute.For<IRepository<Notification>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();

        private ReminderService CreateSut() => new(
            _reminderRepo, _userRepo, _notificationRepo, _unitOfWork, _notificationService, _emailService);

        private static ClaimsPrincipal CallerWithId(int userId, params string[] permissions)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        // ----- CreateReminderAsync -----

        [Fact]
        public async Task CreateReminderAsync_WhenAssignedToSelf_CreatesReminderOwnedByCaller()
        {
            var reminders = new List<Reminder>();
            _reminderRepo.Query().Returns(_ => reminders.BuildMock());
            _reminderRepo.When(r => r.Add(Arg.Any<Reminder>())).Do(ci =>
            {
                var added = ci.Arg<Reminder>();
                added.Id = 1;
                reminders.Add(added);
            });

            var dto = new CreateReminderDto
            {
                Title = "Renew SSL cert",
                DueAt = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
                Priority = "high",
                Channel = "Email"
            };

            var result = await CreateSut().CreateReminderAsync(dto, CallerWithId(10));

            Assert.Equal("Renew SSL cert", result.Title);
            Assert.Equal(10, result.AssignedToId);
            Assert.Equal(10, result.CreatedBy);
            _reminderRepo.Received(1).Add(Arg.Is<Reminder>(x =>
                x.Title == "Renew SSL cert" && x.AssignedToId == 10 && x.CreatedBy == 10));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task CreateReminderAsync_WhenAssigningToAnotherUserWithoutManagePermission_ThrowsUnauthorizedAccessException()
        {
            var sut = CreateSut();
            var dto = new CreateReminderDto { Title = "Task", DueAt = DateTime.UtcNow.AddDays(1), AssignedToId = 20 };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.CreateReminderAsync(dto, CallerWithId(10)));
            _reminderRepo.DidNotReceive().Add(Arg.Any<Reminder>());
        }

        [Fact]
        public async Task CreateReminderAsync_WhenAssigningToAnotherUserWithManagePermission_Succeeds()
        {
            var reminders = new List<Reminder>();
            _reminderRepo.Query().Returns(_ => reminders.BuildMock());
            _reminderRepo.When(r => r.Add(Arg.Any<Reminder>())).Do(ci =>
            {
                var added = ci.Arg<Reminder>();
                added.Id = 1;
                reminders.Add(added);
            });

            var dto = new CreateReminderDto { Title = "Task", DueAt = DateTime.UtcNow.AddDays(1), AssignedToId = 20 };
            var result = await CreateSut().CreateReminderAsync(dto, CallerWithId(10, "reminders.manage"));

            Assert.Equal(20, result.AssignedToId);
            Assert.Equal(10, result.CreatedBy);
        }

        [Theory]
        [InlineData("Yearly")]
        [InlineData("daily")] // recognized types are case-sensitive
        public async Task CreateReminderAsync_WhenRecurrenceTypeIsNotRecognized_ThrowsInvalidOperationException(string recurrenceType)
        {
            var sut = CreateSut();
            var dto = new CreateReminderDto { Title = "Task", DueAt = DateTime.UtcNow.AddDays(1), RecurrenceType = recurrenceType };

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateReminderAsync(dto, CallerWithId(10)));
        }

        [Fact]
        public async Task CreateReminderAsync_WhenRecurrenceEndDateIsBeforeDueAt_ThrowsInvalidOperationException()
        {
            var sut = CreateSut();
            var dueAt = new DateTime(2026, 9, 10);
            var dto = new CreateReminderDto { Title = "Task", DueAt = dueAt, RecurrenceType = "Daily", RecurrenceEndDate = dueAt.AddDays(-1) };

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateReminderAsync(dto, CallerWithId(10)));
        }

        // ----- UpdateReminderAsync -----

        [Fact]
        public async Task UpdateReminderAsync_WhenReminderDoesNotExist_ReturnsFalse()
        {
            _reminderRepo.Query().Returns(new List<Reminder>().BuildMock());

            var updated = await CreateSut().UpdateReminderAsync(999, new UpdateReminderDto { Title = "x", DueAt = DateTime.UtcNow }, CallerWithId(1));

            Assert.False(updated);
        }

        [Fact]
        public async Task UpdateReminderAsync_WhenCallerIsNeitherOwnerNorAssigneeNorHasViewAll_ThrowsUnauthorizedAccessException()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 5, CreatedBy = 5, Title = "Old" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => sut.UpdateReminderAsync(1, new UpdateReminderDto { Title = "New", DueAt = DateTime.UtcNow }, CallerWithId(99)));
        }

        [Fact]
        public async Task UpdateReminderAsync_WhenReassigningWithoutManagePermission_ThrowsUnauthorizedAccessException()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Title = "Old" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var sut = CreateSut();
            var dto = new UpdateReminderDto { Title = "New", DueAt = DateTime.UtcNow, AssignedToId = 20 };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.UpdateReminderAsync(1, dto, CallerWithId(10)));
        }

        [Fact]
        public async Task UpdateReminderAsync_WhenAuthorized_UpdatesFieldsAndReturnsTrue()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Title = "Old", Priority = "low", Channel = "InApp" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var dto = new UpdateReminderDto { Title = "New Title", DueAt = new DateTime(2026, 10, 1), Priority = "high", Channel = "Both", Category = "Ops" };

            var updated = await CreateSut().UpdateReminderAsync(1, dto, CallerWithId(10));

            Assert.True(updated);
            Assert.Equal("New Title", reminder.Title);
            Assert.Equal("high", reminder.Priority);
            Assert.Equal("Both", reminder.Channel);
            Assert.Equal("Ops", reminder.Category);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ----- DeleteReminderAsync -----

        [Fact]
        public async Task DeleteReminderAsync_WhenNotFound_ReturnsFalse()
        {
            _reminderRepo.Query().Returns(new List<Reminder>().BuildMock());

            var deleted = await CreateSut().DeleteReminderAsync(999, CallerWithId(1));

            Assert.False(deleted);
        }

        [Fact]
        public async Task DeleteReminderAsync_WhenCallerLacksAccess_ThrowsUnauthorizedAccessException()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10 };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.DeleteReminderAsync(1, CallerWithId(99)));
        }

        [Fact]
        public async Task DeleteReminderAsync_WhenOwnerDeletes_RemovesReminderAndReturnsTrue()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10 };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            var deleted = await CreateSut().DeleteReminderAsync(1, CallerWithId(10));

            Assert.True(deleted);
            _reminderRepo.Received(1).Remove(reminder);
        }

        // ----- CompleteAsync: recurrence logic -----

        [Fact]
        public async Task CompleteAsync_WhenNotFound_ReturnsFalse()
        {
            _reminderRepo.Query().Returns(new List<Reminder>().BuildMock());

            var completed = await CreateSut().CompleteAsync(999, CallerWithId(1));

            Assert.False(completed);
        }

        [Fact]
        public async Task CompleteAsync_WhenReminderHasNoRecurrence_MarksCompletedAndDoesNotCreateNextOccurrence()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            var completed = await CreateSut().CompleteAsync(1, CallerWithId(10));

            Assert.True(completed);
            Assert.Equal("Completed", reminder.Status);
            Assert.NotNull(reminder.CompletedAt);
            _reminderRepo.DidNotReceive().Add(Arg.Any<Reminder>());
        }

        [Fact]
        public async Task CompleteAsync_WhenDailyRecurrence_CreatesNextOccurrenceOneDayAfterOriginalDueDate()
        {
            var dueAt = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc);
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending", DueAt = dueAt, RecurrenceType = "Daily", Title = "Standup" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            await CreateSut().CompleteAsync(1, CallerWithId(10));

            _reminderRepo.Received(1).Add(Arg.Is<Reminder>(x =>
                x.DueAt == dueAt.AddDays(1) && x.RecurrenceParentId == 1 && x.Title == "Standup"));
        }

        [Fact]
        public async Task CompleteAsync_WhenWeeklyRecurrence_CreatesNextOccurrenceSevenDaysAfterOriginalDueDate()
        {
            var dueAt = new DateTime(2026, 9, 1);
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending", DueAt = dueAt, RecurrenceType = "Weekly" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            await CreateSut().CompleteAsync(1, CallerWithId(10));

            _reminderRepo.Received(1).Add(Arg.Is<Reminder>(x => x.DueAt == dueAt.AddDays(7)));
        }

        [Fact]
        public async Task CompleteAsync_WhenMonthlyRecurrence_CreatesNextOccurrenceOneMonthAfterOriginalDueDate()
        {
            var dueAt = new DateTime(2026, 9, 1);
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending", DueAt = dueAt, RecurrenceType = "Monthly" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            await CreateSut().CompleteAsync(1, CallerWithId(10));

            _reminderRepo.Received(1).Add(Arg.Is<Reminder>(x => x.DueAt == dueAt.AddMonths(1)));
        }

        [Fact]
        public async Task CompleteAsync_WhenNextOccurrenceWouldBeAfterRecurrenceEndDate_DoesNotCreateNextOccurrence()
        {
            var dueAt = new DateTime(2026, 9, 1);
            // Daily recurrence -> next occurrence is dueAt + 1 day, which lands after an end date equal to dueAt itself.
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending", DueAt = dueAt, RecurrenceType = "Daily", RecurrenceEndDate = dueAt };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            await CreateSut().CompleteAsync(1, CallerWithId(10));

            _reminderRepo.DidNotReceive().Add(Arg.Any<Reminder>());
        }

        [Fact]
        public async Task CompleteAsync_WhenNextOccurrenceLandsExactlyOnRecurrenceEndDate_CreatesNextOccurrence()
        {
            var dueAt = new DateTime(2026, 9, 1);
            // Next occurrence (dueAt + 1 day) equals the end date exactly - the rule only rejects
            // strictly-after, so this boundary case must still create the occurrence.
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending", DueAt = dueAt, RecurrenceType = "Daily", RecurrenceEndDate = dueAt.AddDays(1) };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            await CreateSut().CompleteAsync(1, CallerWithId(10));

            _reminderRepo.Received(1).Add(Arg.Any<Reminder>());
        }

        [Fact]
        public async Task CompleteAsync_WhenMaxOccurrencesAlreadyReached_DoesNotCreateNextOccurrence()
        {
            var original = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending", DueAt = new DateTime(2026, 9, 1), RecurrenceType = "Daily", RecurrenceMaxOccurrences = 2 };
            var priorOccurrence = new Reminder { Id = 2, AssignedToId = 10, CreatedBy = 10, RecurrenceType = "Daily", RecurrenceParentId = 1 };
            _reminderRepo.Query().Returns(new List<Reminder> { original, priorOccurrence }.BuildMock());

            await CreateSut().CompleteAsync(1, CallerWithId(10));

            _reminderRepo.DidNotReceive().Add(Arg.Any<Reminder>());
        }

        [Fact]
        public async Task CompleteAsync_WhenMaxOccurrencesNotYetReached_CreatesNextOccurrence()
        {
            var original = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending", DueAt = new DateTime(2026, 9, 1), RecurrenceType = "Daily", RecurrenceMaxOccurrences = 3 };
            var priorOccurrence = new Reminder { Id = 2, AssignedToId = 10, CreatedBy = 10, RecurrenceType = "Daily", RecurrenceParentId = 1 };
            _reminderRepo.Query().Returns(new List<Reminder> { original, priorOccurrence }.BuildMock());

            await CreateSut().CompleteAsync(1, CallerWithId(10));

            _reminderRepo.Received(1).Add(Arg.Any<Reminder>());
        }

        [Fact]
        public async Task CompleteAsync_WhenCompletingAnAlreadyRecurringOccurrence_NextOccurrencePreservesRootParentId()
        {
            // This reminder (Id=5) is itself a prior occurrence spawned from root reminder Id=1.
            var occurrence = new Reminder { Id = 5, AssignedToId = 10, CreatedBy = 10, Status = "Pending", DueAt = new DateTime(2026, 9, 3), RecurrenceType = "Daily", RecurrenceParentId = 1 };
            _reminderRepo.Query().Returns(new List<Reminder> { occurrence }.BuildMock());

            await CreateSut().CompleteAsync(5, CallerWithId(10));

            _reminderRepo.Received(1).Add(Arg.Is<Reminder>(x => x.RecurrenceParentId == 1));
        }

        [Fact]
        public async Task CompleteAsync_WhenCallerLacksAccess_ThrowsUnauthorizedAccessException()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.CompleteAsync(1, CallerWithId(99)));
        }

        // ----- ReopenAsync -----

        [Fact]
        public async Task ReopenAsync_WhenNotFound_ReturnsFalse()
        {
            _reminderRepo.Query().Returns(new List<Reminder>().BuildMock());

            var result = await CreateSut().ReopenAsync(999, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task ReopenAsync_ClearsSnoozeAndCompletionAndSetsStatusPending()
        {
            var reminder = new Reminder
            {
                Id = 1,
                AssignedToId = 10,
                CreatedBy = 10,
                Status = "Snoozed",
                SnoozedUntil = DateTime.UtcNow.AddDays(1),
                CompletedAt = DateTime.UtcNow.AddDays(-1)
            };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            var result = await CreateSut().ReopenAsync(1, CallerWithId(10));

            Assert.True(result);
            Assert.Equal("Pending", reminder.Status);
            Assert.Null(reminder.SnoozedUntil);
            Assert.Null(reminder.CompletedAt);
        }

        // ----- SnoozeAsync -----

        [Fact]
        public async Task SnoozeAsync_WhenNotFound_ReturnsFalse()
        {
            _reminderRepo.Query().Returns(new List<Reminder>().BuildMock());

            var result = await CreateSut().SnoozeAsync(999, new SnoozeReminderDto { SnoozeUntil = DateTime.UtcNow.AddDays(1) }, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task SnoozeAsync_SetsStatusSnoozedAndPushesSnoozedUntilForward()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var snoozeUntil = new DateTime(2026, 9, 5, 8, 0, 0);

            var result = await CreateSut().SnoozeAsync(1, new SnoozeReminderDto { SnoozeUntil = snoozeUntil }, CallerWithId(10));

            Assert.True(result);
            Assert.Equal("Snoozed", reminder.Status);
            Assert.Equal(snoozeUntil, reminder.SnoozedUntil);
        }

        [Fact]
        public async Task SnoozeAsync_WhenCallerLacksAccess_ThrowsUnauthorizedAccessException()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => sut.SnoozeAsync(1, new SnoozeReminderDto { SnoozeUntil = DateTime.UtcNow.AddDays(1) }, CallerWithId(99)));
        }

        // ----- DuplicateAsync -----

        [Fact]
        public async Task DuplicateAsync_WhenNotFound_ThrowsInvalidOperationException()
        {
            _reminderRepo.Query().Returns(new List<Reminder>().BuildMock());
            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DuplicateAsync(999, CallerWithId(10)));
        }

        [Fact]
        public async Task DuplicateAsync_WhenCallerLacksAccess_ThrowsUnauthorizedAccessException()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10 };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.DuplicateAsync(1, CallerWithId(99)));
        }

        [Fact]
        public async Task DuplicateAsync_CopiesReminderFieldsAndSetsCreatedByToCaller()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Title = "Original", Priority = "high", AssignedToId = 10, CreatedBy = 10, Channel = "Email", DueAt = new DateTime(2026, 9, 1), RecurrenceType = "Weekly" }
            };
            _reminderRepo.Query().Returns(_ => reminders.BuildMock());
            _reminderRepo.When(r => r.Add(Arg.Any<Reminder>())).Do(ci =>
            {
                var added = ci.Arg<Reminder>();
                added.Id = 2;
                reminders.Add(added);
            });

            // Caller 99 duplicates a reminder owned by user 10, using reminders.view_all to gain access.
            var result = await CreateSut().DuplicateAsync(1, CallerWithId(99, "reminders.view_all"));

            Assert.Equal("Original", result.Title);
            Assert.Equal(99, result.CreatedBy);
            Assert.Equal(10, result.AssignedToId);
            _reminderRepo.Received(1).Add(Arg.Is<Reminder>(x =>
                x.Title == "Original" && x.CreatedBy == 99 && x.AssignedToId == 10 && x.RecurrenceType == "Weekly"));
        }

        // ----- Bulk actions -----

        [Fact]
        public async Task BulkCompleteAsync_OnlyAffectsRemindersCallerCanAccessAndReturnsAffectedCount()
        {
            var own = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Status = "Pending" };
            var others = new Reminder { Id = 2, AssignedToId = 20, CreatedBy = 20, Status = "Pending" };
            _reminderRepo.Query().Returns(new List<Reminder> { own, others }.BuildMock());

            var count = await CreateSut().BulkCompleteAsync(new BulkReminderActionDto { Ids = new List<int> { 1, 2 } }, CallerWithId(10));

            Assert.Equal(1, count);
            Assert.Equal("Completed", own.Status);
            Assert.Equal("Pending", others.Status);
        }

        [Fact]
        public async Task BulkDeleteAsync_OnlyRemovesAccessibleReminders()
        {
            var own = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10 };
            var others = new Reminder { Id = 2, AssignedToId = 20, CreatedBy = 20 };
            _reminderRepo.Query().Returns(new List<Reminder> { own, others }.BuildMock());

            var count = await CreateSut().BulkDeleteAsync(new BulkReminderActionDto { Ids = new List<int> { 1, 2 } }, CallerWithId(10));

            Assert.Equal(1, count);
            _reminderRepo.Received(1).RemoveRange(Arg.Is<IEnumerable<Reminder>>(rows => rows.Count() == 1 && rows.First().Id == 1));
        }

        [Fact]
        public async Task BulkRescheduleAsync_UpdatesDueAtOnlyForAccessibleReminders()
        {
            var own = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) };
            var others = new Reminder { Id = 2, AssignedToId = 20, CreatedBy = 20, DueAt = new DateTime(2026, 1, 1) };
            _reminderRepo.Query().Returns(new List<Reminder> { own, others }.BuildMock());
            var newDue = new DateTime(2026, 12, 25);

            var count = await CreateSut().BulkRescheduleAsync(new BulkRescheduleReminderDto { Ids = new List<int> { 1, 2 }, DueAt = newDue }, CallerWithId(10));

            Assert.Equal(1, count);
            Assert.Equal(newDue, own.DueAt);
            Assert.Equal(new DateTime(2026, 1, 1), others.DueAt);
        }

        [Fact]
        public async Task BulkPriorityAsync_WhenPriorityIsInvalid_ThrowsInvalidOperationException()
        {
            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.BulkPriorityAsync(new BulkPriorityReminderDto { Ids = new List<int> { 1 }, Priority = "urgent" }, CallerWithId(10)));
        }

        [Fact]
        public async Task BulkPriorityAsync_UpdatesPriorityForAccessibleReminders()
        {
            var own = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10, Priority = "low" };
            _reminderRepo.Query().Returns(new List<Reminder> { own }.BuildMock());

            var count = await CreateSut().BulkPriorityAsync(new BulkPriorityReminderDto { Ids = new List<int> { 1 }, Priority = "high" }, CallerWithId(10));

            Assert.Equal(1, count);
            Assert.Equal("high", own.Priority);
        }

        [Fact]
        public async Task BulkAssignAsync_ReassignsRemindersRegardlessOfCallerOwnership()
        {
            // BulkAssignAsync is reminders.manage-gated at the controller, not filtered by ownership here -
            // it must reassign every targeted reminder even ones the caller neither owns nor created.
            var notOwnedByCaller = new Reminder { Id = 1, AssignedToId = 20, CreatedBy = 20 };
            _reminderRepo.Query().Returns(new List<Reminder> { notOwnedByCaller }.BuildMock());

            var count = await CreateSut().BulkAssignAsync(new BulkAssignReminderDto { Ids = new List<int> { 1 }, AssignedToId = 30 }, CallerWithId(10));

            Assert.Equal(1, count);
            Assert.Equal(30, notOwnedByCaller.AssignedToId);
        }

        // ----- GetReminderByIdAsync -----

        [Fact]
        public async Task GetReminderByIdAsync_WhenNotFound_ReturnsNull()
        {
            _reminderRepo.Query().Returns(new List<Reminder>().BuildMock());

            var result = await CreateSut().GetReminderByIdAsync(999, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetReminderByIdAsync_WhenCallerLacksAccess_ThrowsUnauthorizedAccessException()
        {
            var reminder = new Reminder { Id = 1, AssignedToId = 10, CreatedBy = 10 };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.GetReminderByIdAsync(1, CallerWithId(99)));
        }

        [Fact]
        public async Task GetReminderByIdAsync_WhenOwner_ReturnsMappedDto()
        {
            var reminder = new Reminder
            {
                Id = 1,
                Title = "X",
                AssignedToId = 10,
                CreatedBy = 10,
                AssignedTo = new User { Id = 10, Name = "Alice" },
                Creator = new User { Id = 10, Name = "Alice" }
            };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());

            var result = await CreateSut().GetReminderByIdAsync(1, CallerWithId(10));

            Assert.NotNull(result);
            Assert.Equal("X", result!.Title);
            Assert.Equal("Alice", result.AssignedToName);
        }

        // ----- GetRemindersAsync: ownership and filters -----

        [Fact]
        public async Task GetRemindersAsync_WhenCallerCannotViewAll_OnlyReturnsOwnedOrAssignedReminders()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Title = "Mine (assigned)", AssignedToId = 10, CreatedBy = 20, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, Title = "Mine (created)", AssignedToId = 30, CreatedBy = 10, DueAt = new DateTime(2026, 1, 2) },
                new() { Id = 3, Title = "Not mine", AssignedToId = 20, CreatedBy = 20, DueAt = new DateTime(2026, 1, 3) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetRemindersAsync(new ReminderFilterDto(), CallerWithId(10));

            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(result, r => r.Id == 3);
        }

        [Fact]
        public async Task GetRemindersAsync_WhenCallerCanViewAll_ReturnsEveryReminder()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, AssignedToId = 20, CreatedBy = 20, DueAt = new DateTime(2026, 1, 2) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetRemindersAsync(new ReminderFilterDto(), CallerWithId(10, "reminders.view_all"));

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetRemindersAsync_FiltersBySearchTerm_MatchingTitleOrDescription()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Title = "Renew domain", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, Title = "Pay invoice", Description = "Renew domain mentioned here too", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 3, Title = "Unrelated task", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetRemindersAsync(new ReminderFilterDto { Search = "Renew" }, CallerWithId(10, "reminders.view_all"));

            Assert.Equal(new[] { 1, 2 }, result.Select(r => r.Id).OrderBy(id => id).ToArray());
        }

        [Fact]
        public async Task GetRemindersAsync_FiltersByStatusPriorityAndCategory()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Status = "Pending", Priority = "high", Category = "Ops", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, Status = "Completed", Priority = "high", Category = "Ops", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 3, Status = "Pending", Priority = "low", Category = "Finance", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());
            var sut = CreateSut();
            var caller = CallerWithId(10, "reminders.view_all");

            var byStatus = await sut.GetRemindersAsync(new ReminderFilterDto { Status = "Completed" }, caller);
            Assert.Equal(new[] { 2 }, byStatus.Select(r => r.Id));

            var byPriority = await sut.GetRemindersAsync(new ReminderFilterDto { Priority = "low" }, caller);
            Assert.Equal(new[] { 3 }, byPriority.Select(r => r.Id));

            var byCategory = await sut.GetRemindersAsync(new ReminderFilterDto { Category = "Finance" }, caller);
            Assert.Equal(new[] { 3 }, byCategory.Select(r => r.Id));
        }

        [Fact]
        public async Task GetRemindersAsync_FiltersByExplicitDueDateRange()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 6, 1) },
                new() { Id = 3, AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 12, 1) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetRemindersAsync(new ReminderFilterDto
            {
                DueFrom = new DateTime(2026, 2, 1),
                DueTo = new DateTime(2026, 7, 1)
            }, CallerWithId(10, "reminders.view_all"));

            Assert.Equal(new[] { 2 }, result.Select(r => r.Id));
        }

        [Fact]
        public async Task GetRemindersAsync_FiltersByHasRecurrence()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1), RecurrenceType = "Daily" },
                new() { Id = 2, AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1), RecurrenceType = null },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());
            var sut = CreateSut();
            var caller = CallerWithId(10, "reminders.view_all");

            var recurring = await sut.GetRemindersAsync(new ReminderFilterDto { HasRecurrence = true }, caller);
            Assert.Equal(new[] { 1 }, recurring.Select(r => r.Id));

            var nonRecurring = await sut.GetRemindersAsync(new ReminderFilterDto { HasRecurrence = false }, caller);
            Assert.Equal(new[] { 2 }, nonRecurring.Select(r => r.Id));
        }

        [Fact]
        public async Task GetRemindersAsync_FiltersByExplicitAssignedToIdAndCreatedBy()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, AssignedToId = 10, CreatedBy = 20, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, AssignedToId = 30, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());
            var sut = CreateSut();
            var caller = CallerWithId(10, "reminders.view_all");

            var byAssignee = await sut.GetRemindersAsync(new ReminderFilterDto { AssignedToId = 10 }, caller);
            Assert.Equal(new[] { 1 }, byAssignee.Select(r => r.Id));

            var byCreator = await sut.GetRemindersAsync(new ReminderFilterDto { CreatedBy = 10 }, caller);
            Assert.Equal(new[] { 2 }, byCreator.Select(r => r.Id));
        }

        [Fact]
        public async Task GetRemindersAsync_ViewCompleted_ReturnsOnlyCompletedReminders()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Status = "Completed", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
                new() { Id = 2, Status = "Pending", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetRemindersAsync(new ReminderFilterDto { View = "completed" }, CallerWithId(10, "reminders.view_all"));

            Assert.Equal(new[] { 1 }, result.Select(r => r.Id));
        }

        [Fact]
        public async Task GetRemindersAsync_SortsIncompleteRemindersBeforeCompletedThenByDueDateWithinEachGroup()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Status = "Completed", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2020, 1, 1) }, // earliest due but completed - must sort last
                new() { Id = 2, Status = "Pending", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 6, 1) },
                new() { Id = 3, Status = "Pending", AssignedToId = 10, CreatedBy = 10, DueAt = new DateTime(2026, 1, 1) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetRemindersAsync(new ReminderFilterDto(), CallerWithId(10, "reminders.view_all"));

            Assert.Equal(new[] { 3, 2, 1 }, result.Select(r => r.Id).ToArray());
        }

        [Fact]
        public async Task GetRemindersAsync_SortsBySnoozedUntilInsteadOfDueAtForSnoozedReminders()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Status = "Snoozed", DueAt = new DateTime(2020, 1, 1), SnoozedUntil = new DateTime(2030, 1, 1), AssignedToId = 10, CreatedBy = 10 },
                new() { Id = 2, Status = "Pending", DueAt = new DateTime(2026, 1, 1), AssignedToId = 10, CreatedBy = 10 },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetRemindersAsync(new ReminderFilterDto(), CallerWithId(10, "reminders.view_all"));

            // Reminder 1's effective due date (its SnoozedUntil, 2030) is later than reminder 2's (2026),
            // so it must sort after reminder 2 despite its raw DueAt (2020) otherwise putting it first.
            Assert.Equal(new[] { 2, 1 }, result.Select(r => r.Id).ToArray());
        }

        // ----- GetSummaryCountsAsync -----

        [Fact]
        public async Task GetSummaryCountsAsync_CountsActiveCompletedAndHighPriorityReminders()
        {
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Status = "Pending", Priority = "high", AssignedToId = 10, CreatedBy = 10, DueAt = DateTime.UtcNow.AddDays(30) },
                new() { Id = 2, Status = "Snoozed", Priority = "low", AssignedToId = 10, CreatedBy = 10, DueAt = DateTime.UtcNow.AddDays(30), SnoozedUntil = DateTime.UtcNow.AddDays(31) },
                new() { Id = 3, Status = "Completed", Priority = "high", AssignedToId = 10, CreatedBy = 10, DueAt = DateTime.UtcNow.AddDays(-30) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetSummaryCountsAsync(CallerWithId(10, "reminders.view_all"));

            Assert.Equal(2, result.TotalActive);
            Assert.Equal(1, result.Completed);
            Assert.Equal(1, result.HighPriority);
        }

        [Fact]
        public async Task GetSummaryCountsAsync_CountsOverdueAndUpcomingUsingEffectiveDue()
        {
            // Offsets are large (10 days) so the outcome is stable regardless of the time-of-day the
            // test happens to run at - only exact same-day boundaries would be flaky, and these aren't.
            var reminders = new List<Reminder>
            {
                new() { Id = 1, Status = "Pending", AssignedToId = 10, CreatedBy = 10, DueAt = DateTime.UtcNow.AddDays(-10) },
                new() { Id = 2, Status = "Pending", AssignedToId = 10, CreatedBy = 10, DueAt = DateTime.UtcNow.AddDays(10) },
                // Snoozed far into the future - must not count as overdue despite its stale DueAt.
                new() { Id = 3, Status = "Snoozed", AssignedToId = 10, CreatedBy = 10, DueAt = DateTime.UtcNow.AddDays(-10), SnoozedUntil = DateTime.UtcNow.AddDays(10) },
            };
            _reminderRepo.Query().Returns(reminders.BuildMock());

            var result = await CreateSut().GetSummaryCountsAsync(CallerWithId(10, "reminders.view_all"));

            Assert.Equal(1, result.Overdue);
            Assert.Equal(2, result.Upcoming);
        }

        // ----- CheckDueRemindersAsync -----

        [Fact]
        public async Task CheckDueRemindersAsync_WhenPendingReminderIsPastDue_CreatesInAppNotification()
        {
            var reminder = new Reminder { Id = 1, Status = "Pending", DueAt = DateTime.UtcNow.AddDays(-1), AssignedToId = 10, Title = "Renew cert", Channel = "InApp" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());

            await CreateSut().CheckDueRemindersAsync();

            await _notificationService.Received(1).CreateNotificationAsync(10, NotificationTypes.ReminderDue, "Reminder due: Renew cert", null, null, null, null, 1);
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenPendingReminderIsNotYetDue_DoesNotCreateNotification()
        {
            var reminder = new Reminder { Id = 1, Status = "Pending", DueAt = DateTime.UtcNow.AddDays(10), AssignedToId = 10, Title = "Future", Channel = "InApp" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());

            await CreateSut().CheckDueRemindersAsync();

            await _notificationService.DidNotReceive().CreateNotificationAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenSnoozedReminderPastSnoozeTime_CreatesNotification()
        {
            var reminder = new Reminder { Id = 1, Status = "Snoozed", DueAt = DateTime.UtcNow.AddDays(-30), SnoozedUntil = DateTime.UtcNow.AddDays(-1), AssignedToId = 10, Title = "Snoozed thing", Channel = "InApp" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());

            await CreateSut().CheckDueRemindersAsync();

            await _notificationService.Received(1).CreateNotificationAsync(10, NotificationTypes.ReminderDue, "Reminder due: Snoozed thing", null, null, null, null, 1);
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenSnoozedReminderStillInTheFuture_DoesNotCreateNotification()
        {
            // Status=Snoozed with a stale (past) DueAt but a future SnoozedUntil must not be treated as
            // due - the query's Pending-branch doesn't apply (wrong status) and the Snoozed-branch checks
            // SnoozedUntil, not DueAt.
            var reminder = new Reminder { Id = 1, Status = "Snoozed", DueAt = DateTime.UtcNow.AddDays(-30), SnoozedUntil = DateTime.UtcNow.AddDays(10), AssignedToId = 10, Title = "Snoozed thing", Channel = "InApp" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());

            await CreateSut().CheckDueRemindersAsync();

            await _notificationService.DidNotReceive().CreateNotificationAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenAlreadyNotifiedWithinLast24Hours_DoesNotCreateDuplicateNotification()
        {
            var reminder = new Reminder { Id = 1, Status = "Pending", DueAt = DateTime.UtcNow.AddDays(-1), AssignedToId = 10, Title = "Renew cert", Channel = "InApp" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>
            {
                new() { ReminderId = 1, Type = NotificationTypes.ReminderDue, CreatedAt = DateTime.UtcNow.AddHours(-1) }
            }.BuildMock());

            await CreateSut().CheckDueRemindersAsync();

            await _notificationService.DidNotReceive().CreateNotificationAsync(
                Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int?>());
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenNotifiedMoreThanADayAgo_CreatesAFreshNotification()
        {
            var reminder = new Reminder { Id = 1, Status = "Pending", DueAt = DateTime.UtcNow.AddDays(-2), AssignedToId = 10, Title = "Renew cert", Channel = "InApp" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>
            {
                new() { ReminderId = 1, Type = NotificationTypes.ReminderDue, CreatedAt = DateTime.UtcNow.AddDays(-2) }
            }.BuildMock());

            await CreateSut().CheckDueRemindersAsync();

            await _notificationService.Received(1).CreateNotificationAsync(10, NotificationTypes.ReminderDue, "Reminder due: Renew cert", null, null, null, null, 1);
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenChannelIsEmailAndEmailNotificationsEnabled_SendsReminderEmail()
        {
            var user = new User { Id = 10, Email = "alice@khoi.dev" };
            var reminder = new Reminder { Id = 1, Status = "Pending", DueAt = DateTime.UtcNow.AddDays(-1), AssignedToId = 10, AssignedTo = user, Title = "Renew cert", Channel = "Email" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());
            _notificationService.IsEmailEnabledAsync(10, NotificationTypes.ReminderDue).Returns(true);

            await CreateSut().CheckDueRemindersAsync();

            await _emailService.Received(1).SendReminderDueEmailAsync("alice@khoi.dev", "Renew cert", reminder.DueAt);
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenChannelIsInApp_NeverSendsEmail()
        {
            var user = new User { Id = 10, Email = "alice@khoi.dev" };
            var reminder = new Reminder { Id = 1, Status = "Pending", DueAt = DateTime.UtcNow.AddDays(-1), AssignedToId = 10, AssignedTo = user, Title = "Renew cert", Channel = "InApp" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());

            await CreateSut().CheckDueRemindersAsync();

            await _emailService.DidNotReceive().SendReminderDueEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>());
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenEmailChannelButEmailNotificationsDisabled_DoesNotSendEmail()
        {
            var user = new User { Id = 10, Email = "alice@khoi.dev" };
            var reminder = new Reminder { Id = 1, Status = "Pending", DueAt = DateTime.UtcNow.AddDays(-1), AssignedToId = 10, AssignedTo = user, Title = "Renew cert", Channel = "Email" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());
            _notificationService.IsEmailEnabledAsync(10, NotificationTypes.ReminderDue).Returns(false);

            await CreateSut().CheckDueRemindersAsync();

            await _emailService.DidNotReceive().SendReminderDueEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>());
        }

        [Fact]
        public async Task CheckDueRemindersAsync_WhenEmailSendThrows_StillCreatesNotificationAndDoesNotPropagate()
        {
            var user = new User { Id = 10, Email = "alice@khoi.dev" };
            var reminder = new Reminder { Id = 1, Status = "Pending", DueAt = DateTime.UtcNow.AddDays(-1), AssignedToId = 10, AssignedTo = user, Title = "Renew cert", Channel = "Email" };
            _reminderRepo.Query().Returns(new List<Reminder> { reminder }.BuildMock());
            _notificationRepo.Query().Returns(new List<Notification>().BuildMock());
            _notificationService.IsEmailEnabledAsync(10, NotificationTypes.ReminderDue).Returns(true);
            _emailService.SendReminderDueEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>())
                .Returns<Task>(_ => throw new InvalidOperationException("SMTP down"));

            // Must not throw - a failed send is swallowed since the in-app notification already saved.
            await CreateSut().CheckDueRemindersAsync();

            await _notificationService.Received(1).CreateNotificationAsync(10, NotificationTypes.ReminderDue, "Reminder due: Renew cert", null, null, null, null, 1);
        }
    }
}
