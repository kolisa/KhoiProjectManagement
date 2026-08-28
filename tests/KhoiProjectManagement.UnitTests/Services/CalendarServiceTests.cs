using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class CalendarServiceTests
    {
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<CompanyEvent> _eventRepo = Substitute.For<IRepository<CompanyEvent>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

        private CalendarService CreateSut() => new(_userRepo, _eventRepo, _unitOfWork);

        // ---- GetFeedAsync: birthdays ----

        [Fact]
        public async Task GetFeedAsync_IncludesOnlyActiveUsersWithABirthdayInRange()
        {
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "In Range", DateOfBirth = new DateTime(1990, 3, 15), IsActive = true },
                new() { Id = 2, Name = "Out Of Range", DateOfBirth = new DateTime(1990, 6, 1), IsActive = true },
                new() { Id = 3, Name = "Inactive But In Range", DateOfBirth = new DateTime(1990, 3, 15), IsActive = false },
                new() { Id = 4, Name = "No Dob", DateOfBirth = null, IsActive = true },
            }.BuildMock());
            _eventRepo.Query().Returns(new List<CompanyEvent>().BuildMock());

            var feed = await CreateSut().GetFeedAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

            var birthday = Assert.Single(feed.Birthdays);
            Assert.Equal(1, birthday.UserId);
            Assert.Equal("In Range", birthday.Name);
            Assert.Equal(3, birthday.Month);
            Assert.Equal(15, birthday.Day);
        }

        [Fact]
        public async Task GetFeedAsync_ClampsALeapDayBirthdayToFeb28InANonLeapYear()
        {
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Leap Baby", DateOfBirth = new DateTime(2000, 2, 29), IsActive = true },
            }.BuildMock());
            _eventRepo.Query().Returns(new List<CompanyEvent>().BuildMock());

            // 2026 is not a leap year, so Feb 29 must resolve to Feb 28.
            var feed = await CreateSut().GetFeedAsync(new DateTime(2026, 2, 28), new DateTime(2026, 2, 28));

            var birthday = Assert.Single(feed.Birthdays);
            Assert.Equal(1, birthday.UserId);
            Assert.Equal(2, birthday.Month);
            Assert.Equal(29, birthday.Day); // the DTO still reports the true DOB month/day...
            // ...but the clamped candidate (Feb 28) is what put it inside a Feb-28-only range, proving
            // the clamp - a Feb 27 range would exclude it entirely.
        }

        [Fact]
        public async Task GetFeedAsync_WhenRangeSpansTwoCalendarYears_IncludesMatchesFromBothYears()
        {
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Late December", DateOfBirth = new DateTime(1985, 12, 25), IsActive = true },
                new() { Id = 2, Name = "Early January", DateOfBirth = new DateTime(1985, 1, 5), IsActive = true },
                new() { Id = 3, Name = "Mid Year", DateOfBirth = new DateTime(1985, 6, 1), IsActive = true },
            }.BuildMock());
            _eventRepo.Query().Returns(new List<CompanyEvent>().BuildMock());

            var feed = await CreateSut().GetFeedAsync(new DateTime(2026, 12, 20), new DateTime(2027, 1, 10));

            Assert.Equal(2, feed.Birthdays.Count);
            Assert.Contains(feed.Birthdays, b => b.UserId == 1);
            Assert.Contains(feed.Birthdays, b => b.UserId == 2);
            Assert.DoesNotContain(feed.Birthdays, b => b.UserId == 3);
        }

        [Fact]
        public async Task GetFeedAsync_WhenNoUsersHaveABirthdayInRange_ReturnsEmptyBirthdayList()
        {
            _userRepo.Query().Returns(new List<User>
            {
                new() { Id = 1, Name = "Elsewhere", DateOfBirth = new DateTime(1990, 6, 1), IsActive = true },
            }.BuildMock());
            _eventRepo.Query().Returns(new List<CompanyEvent>().BuildMock());

            var feed = await CreateSut().GetFeedAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

            Assert.Empty(feed.Birthdays);
        }

        // ---- GetFeedAsync: events ----

        [Fact]
        public async Task GetFeedAsync_ExcludesEventsOutsideTheDateRangeAndReturnsRemainderOrderedByDate()
        {
            var creator = new User { Id = 1, Name = "Admin" };
            _userRepo.Query().Returns(new List<User>().BuildMock());
            _eventRepo.Query().Returns(new List<CompanyEvent>
            {
                new() { Id = 1, Title = "Too Early", EventDate = new DateTime(2026, 2, 28), CreatedBy = 1, Creator = creator },
                new() { Id = 2, Title = "Later In Range", EventDate = new DateTime(2026, 3, 20), CreatedBy = 1, Creator = creator },
                new() { Id = 3, Title = "Earlier In Range", EventDate = new DateTime(2026, 3, 5), CreatedBy = 1, Creator = creator },
                new() { Id = 4, Title = "Too Late", EventDate = new DateTime(2026, 4, 1), CreatedBy = 1, Creator = creator },
            }.BuildMock());

            var feed = await CreateSut().GetFeedAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

            Assert.Equal(new[] { "Earlier In Range", "Later In Range" }, feed.Events.Select(e => e.Title));
        }

        [Fact]
        public async Task GetFeedAsync_MapsSubjectAndCreatorNamesFromTheLoadedNavigations()
        {
            var creator = new User { Id = 1, Name = "Admin" };
            var subject = new User { Id = 2, Name = "Promoted Person" };
            _userRepo.Query().Returns(new List<User>().BuildMock());
            _eventRepo.Query().Returns(new List<CompanyEvent>
            {
                new()
                {
                    Id = 1,
                    Title = "Promotion",
                    EventType = "Promotion",
                    EventDate = new DateTime(2026, 3, 10),
                    CreatedBy = 1,
                    Creator = creator,
                    SubjectUserId = 2,
                    Subject = subject
                }
            }.BuildMock());

            var feed = await CreateSut().GetFeedAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

            var evt = Assert.Single(feed.Events);
            Assert.Equal("Promotion", evt.EventType);
            Assert.Equal("Promoted Person", evt.SubjectName);
            Assert.Equal("Admin", evt.CreatorName);
        }

        [Fact]
        public async Task GetFeedAsync_WhenCreatorNavigationIsNotLoaded_DefaultsCreatorNameToUnknown()
        {
            _userRepo.Query().Returns(new List<User>().BuildMock());
            _eventRepo.Query().Returns(new List<CompanyEvent>
            {
                new() { Id = 1, Title = "Orphaned", EventDate = new DateTime(2026, 3, 10), CreatedBy = 99, Creator = null! }
            }.BuildMock());

            var feed = await CreateSut().GetFeedAsync(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

            var evt = Assert.Single(feed.Events);
            Assert.Equal("Unknown", evt.CreatorName);
            Assert.Null(evt.SubjectName);
        }

        // ---- CreateEventAsync ----

        [Fact]
        public async Task CreateEventAsync_AddsTheEventAndReturnsItMappedWithNavigations()
        {
            var creator = new User { Id = 1, Name = "Admin" };
            var subject = new User { Id = 2, Name = "Promoted Person" };

            CompanyEvent? added = null;
            _eventRepo.When(r => r.Add(Arg.Any<CompanyEvent>())).Do(ci =>
            {
                added = ci.Arg<CompanyEvent>();
                added.Id = 42;
                added.Creator = creator;
                added.Subject = subject;
            });
            _eventRepo.Query().Returns(_ => new List<CompanyEvent> { added! }.BuildMock());

            var dto = new CreateCompanyEventDto
            {
                Title = "Promotion Announcement",
                Description = "Well deserved",
                EventDate = new DateTime(2026, 3, 1),
                EventType = "Promotion",
                SubjectUserId = 2
            };

            var result = await CreateSut().CreateEventAsync(dto, createdBy: 1);

            Assert.Equal(42, result.Id);
            Assert.Equal("Promotion Announcement", result.Title);
            Assert.Equal("Promoted Person", result.SubjectName);
            Assert.Equal("Admin", result.CreatorName);
            _eventRepo.Received(1).Add(Arg.Is<CompanyEvent>(e =>
                e.Title == "Promotion Announcement" &&
                e.Description == "Well deserved" &&
                e.EventType == "Promotion" &&
                e.SubjectUserId == 2 &&
                e.CreatedBy == 1));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ---- UpdateEventAsync ----

        [Fact]
        public async Task UpdateEventAsync_WhenEventDoesNotExist_ReturnsFalse()
        {
            _eventRepo.Query().Returns(new List<CompanyEvent>().BuildMock());

            var updated = await CreateSut().UpdateEventAsync(999, new CreateCompanyEventDto { Title = "X", EventType = "Event" });

            Assert.False(updated);
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task UpdateEventAsync_WhenEventExists_UpdatesAllFieldsAndReturnsTrue()
        {
            var existing = new CompanyEvent
            {
                Id = 1,
                Title = "Old Title",
                Description = "Old Description",
                EventDate = new DateTime(2026, 1, 1),
                EventType = "Event",
                SubjectUserId = null,
                CreatedBy = 1
            };
            _eventRepo.Query().Returns(new List<CompanyEvent> { existing }.BuildMock());

            var dto = new CreateCompanyEventDto
            {
                Title = "New Title",
                Description = "New Description",
                EventDate = new DateTime(2026, 5, 5),
                EventType = "Promotion",
                SubjectUserId = 7
            };

            var updated = await CreateSut().UpdateEventAsync(1, dto);

            Assert.True(updated);
            Assert.Equal("New Title", existing.Title);
            Assert.Equal("New Description", existing.Description);
            Assert.Equal(new DateTime(2026, 5, 5), existing.EventDate);
            Assert.Equal("Promotion", existing.EventType);
            Assert.Equal(7, existing.SubjectUserId);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ---- DeleteEventAsync ----

        [Fact]
        public async Task DeleteEventAsync_WhenEventDoesNotExist_ReturnsFalse()
        {
            _eventRepo.FindAsync(999).Returns((CompanyEvent?)null);

            var deleted = await CreateSut().DeleteEventAsync(999);

            Assert.False(deleted);
            _eventRepo.DidNotReceive().Remove(Arg.Any<CompanyEvent>());
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteEventAsync_WhenEventExists_RemovesItAndReturnsTrue()
        {
            var existing = new CompanyEvent { Id = 1, Title = "To Delete", CreatedBy = 1 };
            _eventRepo.FindAsync(1).Returns(existing);

            var deleted = await CreateSut().DeleteEventAsync(1);

            Assert.True(deleted);
            _eventRepo.Received(1).Remove(existing);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ---- SetDateOfBirthAsync ----

        [Fact]
        public async Task SetDateOfBirthAsync_WhenUserDoesNotExist_ReturnsFalse()
        {
            _userRepo.FindAsync(999).Returns((User?)null);

            var updated = await CreateSut().SetDateOfBirthAsync(999, new DateTime(1990, 1, 1));

            Assert.False(updated);
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task SetDateOfBirthAsync_WhenUserExists_SetsDateOfBirthAndReturnsTrue()
        {
            var user = new User { Id = 1, Name = "Someone" };
            _userRepo.FindAsync(1).Returns(user);

            var updated = await CreateSut().SetDateOfBirthAsync(1, new DateTime(1990, 4, 12));

            Assert.True(updated);
            Assert.Equal(new DateTime(1990, 4, 12), user.DateOfBirth);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }
    }
}
